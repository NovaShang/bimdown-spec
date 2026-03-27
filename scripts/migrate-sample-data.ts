/**
 * Migrate sample_data from old format to new schema:
 * 1. wall.csv: add thickness (from SVG stroke-width)
 * 2. door/window CSV: add position (0-1 along host wall), delete SVG
 * 3. space.csv: add x,y (centroid of polygon), delete SVG
 * 4. material: map free-text to enum values
 */
import { readFileSync, writeFileSync, readdirSync, existsSync, unlinkSync, statSync } from 'node:fs';
import { join } from 'node:path';

// ─── Material mapping ───────────────────────────────────────────────────────

const MATERIAL_MAP: Record<string, string> = {
  'Metal Stud Layer': 'steel',
  'Metal Stud': 'steel',
  'Softwood, Lumber': 'wood',
  'Concrete, Cast-in-Place gray': 'concrete',
  'Concrete, Cast-in-Place': 'concrete',
  'Concrete': 'concrete',
  'Aluminum': 'aluminum',
  'Glass': 'glass',
  'Steel': 'steel',
  'Wood': 'wood',
  'Brick': 'brick',
  'Stone': 'stone',
  'Default Wall': 'concrete',
  'Gypsum Wall Board': 'gypsum',
  'Copper': 'copper',
};

function mapMaterial(raw: string): string {
  if (!raw) return '';
  const mapped = MATERIAL_MAP[raw];
  if (mapped) return mapped;
  // Try case-insensitive match
  const lower = raw.toLowerCase();
  for (const [k, v] of Object.entries(MATERIAL_MAP)) {
    if (k.toLowerCase() === lower) return v;
  }
  // Try partial match
  if (lower.includes('concrete')) return 'concrete';
  if (lower.includes('steel') || lower.includes('metal')) return 'steel';
  if (lower.includes('wood') || lower.includes('lumber')) return 'wood';
  if (lower.includes('glass')) return 'glass';
  if (lower.includes('aluminum')) return 'aluminum';
  if (lower.includes('brick')) return 'brick';
  if (lower.includes('gypsum')) return 'gypsum';
  if (lower.includes('copper')) return 'copper';
  if (lower.includes('stone')) return 'stone';
  console.warn(`  Unknown material: "${raw}" → keeping as-is`);
  return raw;
}

// ─── CSV helpers ────────────────────────────────────────────────────────────

interface CsvData {
  headers: string[];
  rows: Record<string, string>[];
}

function readCsv(path: string): CsvData {
  const text = readFileSync(path, 'utf-8').trim();
  const lines = text.split('\n');
  if (lines.length === 0) return { headers: [], rows: [] };

  // Parse CSV respecting quoted fields
  const parseLine = (line: string): string[] => {
    const fields: string[] = [];
    let current = '';
    let inQuotes = false;
    for (let i = 0; i < line.length; i++) {
      const ch = line[i];
      if (ch === '"') {
        if (inQuotes && line[i + 1] === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = !inQuotes;
        }
      } else if (ch === ',' && !inQuotes) {
        fields.push(current);
        current = '';
      } else {
        current += ch;
      }
    }
    fields.push(current);
    return fields;
  };

  const headers = parseLine(lines[0]);
  const rows: Record<string, string>[] = [];
  for (let i = 1; i < lines.length; i++) {
    if (!lines[i].trim()) continue;
    const vals = parseLine(lines[i]);
    const row: Record<string, string> = {};
    for (let j = 0; j < headers.length; j++) {
      row[headers[j]] = vals[j] ?? '';
    }
    rows.push(row);
  }
  return { headers, rows };
}

function writeCsv(path: string, data: CsvData): void {
  const escape = (v: string) => v.includes(',') || v.includes('"') ? `"${v.replace(/"/g, '""')}"` : v;
  const lines = [data.headers.join(',')];
  for (const row of data.rows) {
    lines.push(data.headers.map(h => escape(row[h] ?? '')).join(','));
  }
  writeFileSync(path, lines.join('\n') + '\n', 'utf-8');
}

// ─── SVG parsing (minimal) ─────────────────────────────────────────────────

interface SvgLine {
  id: string;
  x1: number; y1: number;
  x2: number; y2: number;
  strokeWidth: number;
}

interface SvgPolygon {
  id: string;
  points: { x: number; y: number }[];
}

function parseSvgLines(path: string): SvgLine[] {
  const text = readFileSync(path, 'utf-8');
  const lines: SvgLine[] = [];
  const re = /<line\s+([^>]+)\/>/g;
  let m;
  while ((m = re.exec(text)) !== null) {
    const attrs = m[1];
    const get = (name: string) => {
      const r = new RegExp(`${name}="([^"]*)"`)
      const match = attrs.match(r);
      return match ? match[1] : '';
    };
    lines.push({
      id: get('id'),
      x1: parseFloat(get('x1')), y1: parseFloat(get('y1')),
      x2: parseFloat(get('x2')), y2: parseFloat(get('y2')),
      strokeWidth: parseFloat(get('stroke-width') || '0'),
    });
  }
  return lines;
}

function parseSvgPolygons(path: string): SvgPolygon[] {
  const text = readFileSync(path, 'utf-8');
  const polys: SvgPolygon[] = [];
  const re = /<polygon\s+([^>]+)\/>/g;
  let m;
  while ((m = re.exec(text)) !== null) {
    const attrs = m[1];
    const idMatch = attrs.match(/id="([^"]*)"/);
    const pointsMatch = attrs.match(/points="([^"]*)"/);
    if (!idMatch || !pointsMatch) continue;
    const points = pointsMatch[1].trim().split(/\s+/).map(p => {
      const [x, y] = p.split(',').map(Number);
      return { x, y };
    });
    polys.push({ id: idMatch[1], points });
  }
  return polys;
}

// ─── Migration logic ────────────────────────────────────────────────────────

function computePosition(
  doorLine: SvgLine,
  wallLine: SvgLine,
): number {
  // Center of the door line
  const cx = (doorLine.x1 + doorLine.x2) / 2;
  const cy = (doorLine.y1 + doorLine.y2) / 2;

  // Project onto wall line
  const wx = wallLine.x2 - wallLine.x1;
  const wy = wallLine.y2 - wallLine.y1;
  const wallLen = Math.sqrt(wx * wx + wy * wy);
  if (wallLen === 0) return 0.5;

  const dx = cx - wallLine.x1;
  const dy = cy - wallLine.y1;
  const t = (dx * wx + dy * wy) / (wallLen * wallLen);
  return Math.max(0, Math.min(1, parseFloat(t.toFixed(4))));
}

function polygonCentroid(pts: { x: number; y: number }[]): { x: number; y: number } {
  let cx = 0, cy = 0;
  for (const p of pts) { cx += p.x; cy += p.y; }
  return { x: parseFloat((cx / pts.length).toFixed(3)), y: parseFloat((cy / pts.length).toFixed(3)) };
}

function migrateLevel(levelDir: string): void {
  // ── Build wall geometry lookup ──
  const wallSvgPath = join(levelDir, 'wall.svg');
  const wallLines = new Map<string, SvgLine>();
  if (existsSync(wallSvgPath)) {
    for (const line of parseSvgLines(wallSvgPath)) {
      wallLines.set(line.id, line);
    }
  }

  // ── 1. Wall: add thickness from SVG stroke-width, map material ──
  const wallCsvPath = join(levelDir, 'wall.csv');
  if (existsSync(wallCsvPath)) {
    const csv = readCsv(wallCsvPath);
    // Add thickness column if not present
    if (!csv.headers.includes('thickness')) {
      csv.headers.push('thickness');
    }
    for (const row of csv.rows) {
      const wall = wallLines.get(row.id);
      row.thickness = wall ? wall.strokeWidth.toFixed(3) : '0.2';
      if (row.material) row.material = mapMaterial(row.material);
    }
    writeCsv(wallCsvPath, csv);
  }

  // ── 2. Door: add position, map material, delete SVG ──
  const doorCsvPath = join(levelDir, 'door.csv');
  const doorSvgPath = join(levelDir, 'door.svg');
  if (existsSync(doorCsvPath)) {
    const csv = readCsv(doorCsvPath);
    const doorLines = existsSync(doorSvgPath)
      ? new Map(parseSvgLines(doorSvgPath).map(l => [l.id, l]))
      : new Map<string, SvgLine>();

    if (!csv.headers.includes('position')) {
      // Insert after host_id
      const idx = csv.headers.indexOf('host_id');
      csv.headers.splice(idx + 1, 0, 'position');
    }
    for (const row of csv.rows) {
      const doorLine = doorLines.get(row.id);
      const hostWall = wallLines.get(row.host_id);
      if (doorLine && hostWall) {
        row.position = String(computePosition(doorLine, hostWall));
      } else {
        row.position = '0.5';
      }
      if (row.material) row.material = mapMaterial(row.material);
    }
    writeCsv(doorCsvPath, csv);
    if (existsSync(doorSvgPath)) unlinkSync(doorSvgPath);
  }

  // ── 3. Window: add position, map material, delete SVG ──
  const windowCsvPath = join(levelDir, 'window.csv');
  const windowSvgPath = join(levelDir, 'window.svg');
  if (existsSync(windowCsvPath)) {
    const csv = readCsv(windowCsvPath);
    const windowLines = existsSync(windowSvgPath)
      ? new Map(parseSvgLines(windowSvgPath).map(l => [l.id, l]))
      : new Map<string, SvgLine>();

    if (!csv.headers.includes('position')) {
      const idx = csv.headers.indexOf('host_id');
      csv.headers.splice(idx + 1, 0, 'position');
    }
    for (const row of csv.rows) {
      const winLine = windowLines.get(row.id);
      const hostWall = wallLines.get(row.host_id);
      if (winLine && hostWall) {
        row.position = String(computePosition(winLine, hostWall));
      } else {
        row.position = '0.5';
      }
      if (row.material) row.material = mapMaterial(row.material);
    }
    writeCsv(windowCsvPath, csv);
    if (existsSync(windowSvgPath)) unlinkSync(windowSvgPath);
  }

  // ── 4. Space: add x,y centroid, delete SVG ──
  const spaceCsvPath = join(levelDir, 'space.csv');
  const spaceSvgPath = join(levelDir, 'space.svg');
  if (existsSync(spaceCsvPath)) {
    const csv = readCsv(spaceCsvPath);
    const spacePolygons = existsSync(spaceSvgPath)
      ? new Map(parseSvgPolygons(spaceSvgPath).map(p => [p.id, p]))
      : new Map<string, SvgPolygon>();

    if (!csv.headers.includes('x')) {
      // Insert before 'name' or at end
      const nameIdx = csv.headers.indexOf('name');
      if (nameIdx >= 0) {
        csv.headers.splice(nameIdx, 0, 'x', 'y');
      } else {
        csv.headers.push('x', 'y');
      }
    }
    for (const row of csv.rows) {
      const poly = spacePolygons.get(row.id);
      if (poly && poly.points.length > 0) {
        const c = polygonCentroid(poly.points);
        row.x = String(c.x);
        row.y = String(c.y);
      } else {
        row.x = '0';
        row.y = '0';
      }
    }
    writeCsv(spaceCsvPath, csv);
    if (existsSync(spaceSvgPath)) unlinkSync(spaceSvgPath);
  }

  // ── 5. Map materials in other tables ──
  for (const table of ['slab', 'structure_wall', 'structure_slab', 'structure_column', 'raft_foundation', 'isolated_foundation', 'strip_foundation']) {
    const csvPath = join(levelDir, `${table}.csv`);
    if (!existsSync(csvPath)) continue;
    const csv = readCsv(csvPath);
    let changed = false;
    for (const row of csv.rows) {
      if (row.material) {
        const mapped = mapMaterial(row.material);
        if (mapped !== row.material) { row.material = mapped; changed = true; }
      }
    }
    if (changed) writeCsv(csvPath, csv);
  }

  // ── 6. Structure wall: add thickness from SVG if missing ──
  const swCsvPath = join(levelDir, 'structure_wall.csv');
  const swSvgPath = join(levelDir, 'structure_wall.svg');
  if (existsSync(swCsvPath) && existsSync(swSvgPath)) {
    const csv = readCsv(swCsvPath);
    if (!csv.headers.includes('thickness')) {
      csv.headers.push('thickness');
      const swLines = new Map(parseSvgLines(swSvgPath).map(l => [l.id, l]));
      for (const row of csv.rows) {
        const line = swLines.get(row.id);
        row.thickness = line ? line.strokeWidth.toFixed(3) : '0.2';
      }
      writeCsv(swCsvPath, csv);
    }
  }
}

// ─── Main ───────────────────────────────────────────────────────────────────

import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';
const __dirname = dirname(fileURLToPath(import.meta.url));
const sampleDir = join(__dirname, '..', 'sample_data');

for (const modelDir of readdirSync(sampleDir)) {
  const modelPath = join(sampleDir, modelDir);
  if (!statSync(modelPath).isDirectory()) continue;
  console.log(`\nMigrating ${modelDir}/`);

  // Process each level directory
  for (const entry of readdirSync(modelPath)) {
    const entryPath = join(modelPath, entry);
    if (entry === 'global') continue;
    if (!entry.startsWith('lv-')) continue;

    console.log(`  ${entry}/`);
    migrateLevel(entryPath);
  }
}

console.log('\nDone.');
