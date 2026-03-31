import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { readCsv, writeCsv, type CsvData } from '../utils/csv.js';
import {
  parseSvgFile,
  extractLineGeometry,
  extractRectGeometry,
  extractCircleGeometry,
  type SvgElement,
} from '../utils/svg.js';
import { discoverLayout, listFiles } from '../utils/fs.js';
import { buildRegistry, getSpecDir, SVG_FILE_NAMES } from '../schema/registry.js';

const TOLERANCE = 0.01; // 1cm in meters

const CURVE_TABLES = ['duct', 'pipe', 'cable_tray', 'conduit'];
const NODE_TABLES = ['equipment', 'terminal', 'mep_node'];

interface Point3D {
  x: number;
  y: number;
  z: number;
}

interface CurveEndpoint {
  curveId: string;
  side: 'start' | 'end';
  point: Point3D;
  levelDir: string;
  systemType: string;
}

interface NodeEntry {
  levelDir: string;
  tableName: string;
  id: string;
  pos: Point3D;
  /** Bounding box half-extents for fitting containment check */
  halfW: number;
  halfH: number;
}

function dist3d(a: Point3D, b: Point3D): number {
  const dx = a.x - b.x;
  const dy = a.y - b.y;
  const dz = a.z - b.z;
  return Math.sqrt(dx * dx + dy * dy + dz * dz);
}

/** Check if point falls within a node's 2D bounding box (with Z tolerance) */
function insideBBox(point: Point3D, node: NodeEntry): boolean {
  const dz = Math.abs(point.z - node.pos.z);
  if (dz > TOLERANCE) return false;
  const dx = Math.abs(point.x - node.pos.x);
  const dy = Math.abs(point.y - node.pos.y);
  // Use bbox half-extents + tolerance margin
  return dx <= node.halfW + TOLERANCE && dy <= node.halfH + TOLERANCE;
}

export function resolveTopology(dir: string): void {
  buildRegistry(getSpecDir());
  const layout = discoverLayout(dir);

  const allDirs = [
    { name: 'global', path: layout.globalDir },
    ...layout.levelDirs,
  ];

  // ─── Phase 1: Load curves, collect free endpoints ───────
  const freeEndpoints: CurveEndpoint[] = [];
  // Track CSV data for later update: key = "levelDir/tableName"
  const curveData = new Map<string, { csv: CsvData; path: string }>();
  let totalCurves = 0;
  let skippedStart = 0;
  let skippedEnd = 0;

  for (const d of allDirs) {
    if (!existsSync(d.path)) continue;
    const files = listFiles(d.path);

    for (const tableName of CURVE_TABLES) {
      if (!files.includes(`${tableName}.csv`)) continue;
      const svgName = SVG_FILE_NAMES[tableName];
      if (!svgName || !files.includes(`${svgName}.svg`)) continue;

      const csvPath = join(d.path, `${tableName}.csv`);
      const svgPath = join(d.path, `${svgName}.svg`);

      const csv = readCsv(csvPath);
      const key = `${d.name}/${tableName}`;
      curveData.set(key, { csv, path: csvPath });

      let svgElements: SvgElement[];
      try {
        svgElements = parseSvgFile(svgPath).elements;
      } catch {
        continue;
      }

      const geoMap = new Map<string, { start_x: number; start_y: number; end_x: number; end_y: number }>();
      for (const el of svgElements) {
        if (el.tag === 'path') {
          const geo = extractLineGeometry(el);
          geoMap.set(el.id, geo);
        }
      }

      for (const row of csv.rows) {
        const geo = geoMap.get(row.id);
        if (!geo) continue;
        totalCurves++;

        const startZ = parseFloat(row.start_z ?? '0');
        const endZ = parseFloat(row.end_z ?? '0');

        // Only collect endpoints where node ID is not already set
        if (row.start_node_id) {
          skippedStart++;
        } else {
          freeEndpoints.push({
            curveId: row.id,
            side: 'start',
            point: { x: geo.start_x, y: geo.start_y, z: startZ },
            levelDir: d.name,
            systemType: row.system_type ?? '',
          });
        }

        if (row.end_node_id) {
          skippedEnd++;
        } else {
          freeEndpoints.push({
            curveId: row.id,
            side: 'end',
            point: { x: geo.end_x, y: geo.end_y, z: endZ },
            levelDir: d.name,
            systemType: row.system_type ?? '',
          });
        }
      }
    }
  }

  // ─── Phase 2: Load existing node positions + bboxes ─────
  const nodes: NodeEntry[] = [];

  for (const d of allDirs) {
    if (!existsSync(d.path)) continue;
    const files = listFiles(d.path);

    for (const tableName of NODE_TABLES) {
      if (!files.includes(`${tableName}.csv`)) continue;
      const svgName = SVG_FILE_NAMES[tableName];
      if (!svgName || !files.includes(`${svgName}.svg`)) continue;

      const csvPath = join(d.path, `${tableName}.csv`);
      const svgPath = join(d.path, `${svgName}.svg`);
      const csv = readCsv(csvPath);

      let svgElements: SvgElement[];
      try {
        svgElements = parseSvgFile(svgPath).elements;
      } catch {
        continue;
      }

      const posMap = new Map<string, { x: number; y: number; halfW: number; halfH: number }>();
      for (const el of svgElements) {
        if (el.tag === 'rect') {
          const geo = extractRectGeometry(el);
          posMap.set(el.id, { x: geo.x, y: geo.y, halfW: geo.size_x / 2, halfH: geo.size_y / 2 });
        } else if (el.tag === 'circle') {
          const geo = extractCircleGeometry(el);
          const r = geo.size_x / 2;
          posMap.set(el.id, { x: geo.x, y: geo.y, halfW: r, halfH: r });
        }
      }

      for (const row of csv.rows) {
        const p = posMap.get(row.id);
        if (!p) continue;
        const z = parseFloat(row.base_offset ?? '0');
        nodes.push({
          levelDir: d.name,
          tableName,
          id: row.id,
          pos: { x: p.x, y: p.y, z },
          halfW: p.halfW,
          halfH: p.halfH,
        });
      }
    }
  }

  // ─── Phase 3: Match free endpoints to existing fittings ─
  // A free endpoint inside a fitting's bbox → assign that fitting's ID
  const resolvedIds = new Map<string, string>(); // "curveId:side" -> nodeId
  const stillFree: CurveEndpoint[] = [];

  for (const ep of freeEndpoints) {
    let matched: NodeEntry | null = null;
    for (const node of nodes) {
      if (insideBBox(ep.point, node)) {
        matched = node;
        break;
      }
    }

    if (matched) {
      resolvedIds.set(`${ep.curveId}:${ep.side}`, matched.id);
    } else {
      stillFree.push(ep);
    }
  }

  const fittingMatches = resolvedIds.size;

  // ─── Phase 4: Cluster remaining free endpoints → new mep_nodes
  interface Junction {
    point: Point3D;
    endpoints: CurveEndpoint[];
    nodeId: string;
    levelDir: string;
    systemType: string;
  }
  const junctions: Junction[] = [];

  for (const ep of stillFree) {
    let found = false;
    for (const j of junctions) {
      if (dist3d(ep.point, j.point) < TOLERANCE) {
        j.endpoints.push(ep);
        found = true;
        break;
      }
    }
    if (!found) {
      junctions.push({
        point: ep.point,
        endpoints: [ep],
        levelDir: ep.levelDir,
        systemType: ep.systemType,
        nodeId: '',
      });
    }
  }

  // Find max existing mep_node ID number per level
  const maxMnId = new Map<string, number>();
  for (const n of nodes) {
    if (n.tableName === 'mep_node') {
      const num = parseInt(n.id.replace('mn-', ''), 10);
      const current = maxMnId.get(n.levelDir) ?? 0;
      if (num > current) maxMnId.set(n.levelDir, num);
    }
  }

  // Assign IDs and record in resolvedIds
  for (const j of junctions) {
    const current = maxMnId.get(j.levelDir) ?? 0;
    const nextId = current + 1;
    maxMnId.set(j.levelDir, nextId);
    j.nodeId = `mn-${nextId}`;

    for (const ep of j.endpoints) {
      resolvedIds.set(`${ep.curveId}:${ep.side}`, j.nodeId);
    }
  }

  // ─── Phase 5: Write new mep_node entries ────────────────
  const newNodesByLevel = new Map<string, Junction[]>();
  for (const j of junctions) {
    const arr = newNodesByLevel.get(j.levelDir) ?? [];
    arr.push(j);
    newNodesByLevel.set(j.levelDir, arr);
  }

  for (const [levelDir, levelJunctions] of newNodesByLevel) {
    const dirPath = levelDir === 'global' ? layout.globalDir : join(dir, levelDir);
    if (!existsSync(dirPath)) mkdirSync(dirPath, { recursive: true });

    const csvPath = join(dirPath, 'mep_node.csv');
    const svgPath = join(dirPath, 'mep_node.svg');

    // Read existing or create new
    let csv: CsvData;
    if (existsSync(csvPath)) {
      csv = readCsv(csvPath);
    } else {
      csv = { headers: ['id', 'number', 'base_offset', 'system_type'], rows: [] };
    }
    for (const h of ['id', 'number', 'base_offset', 'system_type']) {
      if (!csv.headers.includes(h)) csv.headers.push(h);
    }

    // Read existing SVG elements
    let existingSvgElements: SvgElement[] = [];
    if (existsSync(svgPath)) {
      try {
        existingSvgElements = parseSvgFile(svgPath).elements;
      } catch { /* ignore */ }
    }

    // Add new rows and SVG circle elements
    const newSvgLines: string[] = [];
    const NODE_RADIUS = 0.025; // small circle for auto-generated nodes
    for (const j of levelJunctions) {
      csv.rows.push({
        id: j.nodeId,
        number: '',
        base_offset: String(j.point.z),
        system_type: j.systemType,
      });
      newSvgLines.push(
        `    <circle id="${j.nodeId}" cx="${j.point.x.toFixed(3)}" cy="${j.point.y.toFixed(3)}" r="${NODE_RADIUS}" />`,
      );
    }

    writeCsv(csvPath, csv);

    // Rebuild SVG (existing elements + new circles)
    const existingLines: string[] = [];
    for (const el of existingSvgElements) {
      const attrs = Object.entries(el.attrs).map(([k, v]) => `${k}="${v}"`).join(' ');
      existingLines.push(`    <${el.tag} ${attrs} />`);
    }

    // Compute viewBox
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const el of existingSvgElements) {
      if (el.tag === 'rect') {
        const g = extractRectGeometry(el);
        minX = Math.min(minX, g.x - g.size_x / 2); minY = Math.min(minY, g.y - g.size_y / 2);
        maxX = Math.max(maxX, g.x + g.size_x / 2); maxY = Math.max(maxY, g.y + g.size_y / 2);
      } else if (el.tag === 'circle') {
        const g = extractCircleGeometry(el);
        const r = g.size_x / 2;
        minX = Math.min(minX, g.x - r); minY = Math.min(minY, g.y - r);
        maxX = Math.max(maxX, g.x + r); maxY = Math.max(maxY, g.y + r);
      }
    }
    for (const j of levelJunctions) {
      minX = Math.min(minX, j.point.x - NODE_RADIUS); minY = Math.min(minY, j.point.y - NODE_RADIUS);
      maxX = Math.max(maxX, j.point.x + NODE_RADIUS); maxY = Math.max(maxY, j.point.y + NODE_RADIUS);
    }

    let viewBox = '0 0 1 1';
    if (minX !== Infinity) {
      const pad = Math.max(maxX - minX, maxY - minY) * 0.02;
      viewBox = `${(minX - pad).toFixed(3)} ${(-(maxY + pad)).toFixed(3)} ${(maxX - minX + pad * 2).toFixed(3)} ${(maxY - minY + pad * 2).toFixed(3)}`;
    }

    writeFileSync(svgPath, [
      '<?xml version="1.0" encoding="utf-8"?>',
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}">`,
      '  <g transform="scale(1,-1)">',
      ...existingLines,
      ...newSvgLines,
      '  </g>',
      '</svg>',
    ].join('\n'), 'utf-8');
  }

  // ─── Phase 6: Update curve CSVs ─────────────────────────
  let updatedRows = 0;

  for (const [, entry] of curveData) {
    let modified = false;
    const csv = entry.csv;

    if (!csv.headers.includes('start_node_id')) csv.headers.push('start_node_id');
    if (!csv.headers.includes('end_node_id')) csv.headers.push('end_node_id');

    for (const row of csv.rows) {
      // Only fill empty slots — never overwrite Revit-resolved connections
      if (!row.start_node_id) {
        const id = resolvedIds.get(`${row.id}:start`);
        if (id) { row.start_node_id = id; modified = true; updatedRows++; }
      }
      if (!row.end_node_id) {
        const id = resolvedIds.get(`${row.id}:end`);
        if (id) { row.end_node_id = id; modified = true; }
      }
    }

    if (modified) {
      writeCsv(entry.path, csv);
    }
  }

  // ─── Summary ────────────────────────────────────────────
  const totalEndpoints = totalCurves * 2;
  const alreadyResolved = skippedStart + skippedEnd;
  console.log(`Scanned ${totalCurves} MEP curves (${totalEndpoints} endpoints)`);
  console.log(`  Already connected: ${alreadyResolved} (from Revit export)`);
  console.log(`  Free endpoints: ${freeEndpoints.length}`);
  console.log(`  Matched to existing fittings (bbox): ${fittingMatches}`);
  console.log(`  New mep_nodes created: ${junctions.length}`);
  console.log(`  Unresolved: ${stillFree.length - junctions.reduce((s, j) => s + j.endpoints.length, 0)}`);
  if (updatedRows > 0) {
    console.log(`Updated ${updatedRows} curve rows`);
  }
}
