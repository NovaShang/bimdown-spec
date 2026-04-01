/**
 * Renders a BimDown project level to a composite SVG floor plan.
 * Reads all SVG files for a level, normalizes geometry, and produces
 * a single colored SVG with walls, doors, windows, slabs, spaces, etc.
 */
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { discoverLayout } from './utils/fs.js';
import { readCsv } from './utils/csv.js';
import { parseSvgFile, type SvgElement } from './utils/svg.js';
import { SVG_FILE_NAMES } from './schema/registry.js';

const COLORS: Record<string, { stroke: string; fill?: string }> = {
  wall:               { stroke: '#1a1a2e' },
  structure_wall:     { stroke: '#4a4e69' },
  column:             { stroke: '#2b2d42', fill: '#2b2d42' },
  structure_column:   { stroke: '#6c757d', fill: '#6c757d' },
  slab:               { stroke: '#adb5bd', fill: 'rgba(173,181,189,0.2)' },
  structure_slab:     { stroke: '#868e96', fill: 'rgba(134,142,150,0.2)' },
  space:              { stroke: '#3a86ff', fill: 'rgba(58,134,255,0.15)' },
  room_separator:     { stroke: '#adb5bd' },
  door:               { stroke: '#e63946' },
  window:             { stroke: '#2a9d8f' },
  stair:              { stroke: '#f4a261', fill: 'rgba(244,162,97,0.2)' },
  beam:               { stroke: '#9b5de5' },
  duct:               { stroke: '#00b4d8' },
  pipe:               { stroke: '#48bfe3' },
  cable_tray:         { stroke: '#90be6d' },
  conduit:            { stroke: '#43aa8b' },
  equipment:          { stroke: '#f94144', fill: 'rgba(249,65,68,0.15)' },
  terminal:           { stroke: '#f3722c', fill: 'rgba(243,114,44,0.15)' },
};

const DEFAULT_COLOR = { stroke: '#666' };

// Attributes we override — filter these from original SVG attrs to avoid duplicates
const OVERRIDE_ATTRS = new Set(['stroke', 'fill', 'stroke-width', 'stroke-linecap']);

// Tables to render in order (back to front)
const RENDER_ORDER = [
  'slab', 'structure_slab',
  'wall', 'structure_wall', 'room_separator',
  'column', 'structure_column',
  'beam', 'brace',
  'stair',
  'duct', 'pipe', 'cable_tray', 'conduit',
  'equipment', 'terminal',
  'door', 'window',
  'space',
];

function elementBounds(el: SvgElement): { minX: number; minY: number; maxX: number; maxY: number } | null {
  switch (el.tag) {
    case 'path': {
      const d = el.attrs.d ?? '';
      const m = d.match(/M\s*(-?[\d.]+)[,\s]+(-?[\d.]+)\s*L\s*(-?[\d.]+)[,\s]+(-?[\d.]+)/);
      if (!m) return null;
      const x1 = parseFloat(m[1]), y1 = parseFloat(m[2]);
      const x2 = parseFloat(m[3]), y2 = parseFloat(m[4]);
      const sw = parseFloat(el.attrs['stroke-width'] ?? '0.2') / 2;
      return { minX: Math.min(x1, x2) - sw, minY: Math.min(y1, y2) - sw, maxX: Math.max(x1, x2) + sw, maxY: Math.max(y1, y2) + sw };
    }
    case 'line': {
      const x1 = parseFloat(el.attrs.x1 ?? '0'), y1 = parseFloat(el.attrs.y1 ?? '0');
      const x2 = parseFloat(el.attrs.x2 ?? '0'), y2 = parseFloat(el.attrs.y2 ?? '0');
      const sw = parseFloat(el.attrs['stroke-width'] ?? '0') / 2;
      return { minX: Math.min(x1, x2) - sw, minY: Math.min(y1, y2) - sw, maxX: Math.max(x1, x2) + sw, maxY: Math.max(y1, y2) + sw };
    }
    case 'rect': {
      const x = parseFloat(el.attrs.x ?? '0'), y = parseFloat(el.attrs.y ?? '0');
      const w = parseFloat(el.attrs.width ?? '0'), h = parseFloat(el.attrs.height ?? '0');
      return { minX: x, minY: y, maxX: x + w, maxY: y + h };
    }
    case 'circle': {
      const cx = parseFloat(el.attrs.cx ?? '0'), cy = parseFloat(el.attrs.cy ?? '0');
      const r = parseFloat(el.attrs.r ?? '0');
      return { minX: cx - r, minY: cy - r, maxX: cx + r, maxY: cy + r };
    }
    case 'polygon': {
      const pts = (el.attrs.points ?? '').trim().split(/\s+/).map((p) => {
        const [x, y] = p.split(',').map(Number);
        return { x: x ?? 0, y: y ?? 0 };
      });
      if (pts.length === 0) return null;
      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
      for (const p of pts) {
        minX = Math.min(minX, p.x); minY = Math.min(minY, p.y);
        maxX = Math.max(maxX, p.x); maxY = Math.max(maxY, p.y);
      }
      return { minX, minY, maxX, maxY };
    }
    default:
      return null;
  }
}

function renderElement(el: SvgElement, tableName: string): string {
  const color = COLORS[tableName] ?? DEFAULT_COLOR;
  const isDashed = tableName === 'room_separator';

  switch (el.tag) {
    case 'path': {
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      const sw = el.attrs['stroke-width'] ?? '0.2';
      const dash = isDashed ? ' stroke-dasharray="0.2,0.1"' : '';
      return `    <path ${attrs} stroke="${color.stroke}" stroke-width="${sw}" stroke-linecap="square"${dash} />`;
    }
    case 'line': {
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      const sw = el.attrs['stroke-width'] ?? '0.2';
      const dash = isDashed ? ' stroke-dasharray="0.2,0.1"' : '';
      return `    <line ${attrs} stroke="${color.stroke}" stroke-width="${sw}" stroke-linecap="square"${dash} />`;
    }
    case 'polygon': {
      const fill = color.fill ?? 'none';
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      return `    <polygon ${attrs} fill="${fill}" stroke="${color.stroke}" stroke-width="0.05" />`;
    }
    case 'rect': {
      const fill = color.fill ?? 'none';
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      return `    <rect ${attrs} fill="${fill}" stroke="${color.stroke}" stroke-width="0.05" />`;
    }
    case 'circle': {
      const fill = color.fill ?? 'none';
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      return `    <circle ${attrs} fill="${fill}" stroke="${color.stroke}" stroke-width="0.05" />`;
    }
    default:
      return '';
  }
}

function escapeXml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// CSV-only element types that are rendered from CSV data, not SVG files
const CSV_ONLY_TABLES = new Set(['door', 'window', 'space']);

interface CsvOnlyElement {
  tableName: string;
  svgMarkup: string;
  bounds: { minX: number; minY: number; maxX: number; maxY: number };
}

/**
 * Generate SVG markup for doors/windows from CSV position + host wall geometry.
 */
function renderHostedElements(
  levelDir: { path: string },
  tableName: string,
  wallElements: Map<string, SvgElement>,
): CsvOnlyElement[] {
  const csvPath = join(levelDir.path, `${tableName}.csv`);
  if (!existsSync(csvPath)) return [];

  const csv = readCsv(csvPath);
  const color = COLORS[tableName] ?? DEFAULT_COLOR;
  const results: CsvOnlyElement[] = [];

  for (const row of csv.rows) {
    const hostId = row.host_id;
    const position = parseFloat(row.position ?? '');
    const width = parseFloat(row.width ?? '0');
    if (!hostId || isNaN(position)) continue;

    const wall = wallElements.get(hostId);
    if (!wall) continue;

    let wx1: number, wy1: number, wx2: number, wy2: number;
    if (wall.tag === 'path') {
      const d = wall.attrs.d ?? '';
      const m = d.match(/M\s*(-?[\d.]+)[,\s]+(-?[\d.]+)\s*L\s*(-?[\d.]+)[,\s]+(-?[\d.]+)/);
      if (!m) continue;
      wx1 = parseFloat(m[1]); wy1 = parseFloat(m[2]);
      wx2 = parseFloat(m[3]); wy2 = parseFloat(m[4]);
    } else {
      wx1 = parseFloat(wall.attrs.x1 ?? '0');
      wy1 = parseFloat(wall.attrs.y1 ?? '0');
      wx2 = parseFloat(wall.attrs.x2 ?? '0');
      wy2 = parseFloat(wall.attrs.y2 ?? '0');
    }

    // Center point along wall at parametric position
    const cx = wx1 + (wx2 - wx1) * position;
    const cy = wy1 + (wy2 - wy1) * position;

    // Direction along wall, normalized
    const dx = wx2 - wx1;
    const dy = wy2 - wy1;
    const len = Math.sqrt(dx * dx + dy * dy);
    if (len === 0) continue;
    const ux = dx / len;
    const uy = dy / len;

    // Opening line: width/2 in each direction along wall
    const halfW = width / 2;
    const x1 = cx - ux * halfW;
    const y1 = cy - uy * halfW;
    const x2 = cx + ux * halfW;
    const y2 = cy + uy * halfW;

    const sw = tableName === 'door' ? '0.08' : '0.06';
    const markup = `    <line id="${row.id}" x1="${x1.toFixed(3)}" y1="${y1.toFixed(3)}" x2="${x2.toFixed(3)}" y2="${y2.toFixed(3)}" stroke="${color.stroke}" stroke-width="${sw}" />`;

    results.push({
      tableName,
      svgMarkup: markup,
      bounds: {
        minX: Math.min(x1, x2),
        minY: Math.min(y1, y2),
        maxX: Math.max(x1, x2),
        maxY: Math.max(y1, y2),
      },
    });
  }

  return results;
}

/**
 * Generate SVG markup for spaces from CSV seed points.
 */
function renderSpaces(levelDir: { path: string }): CsvOnlyElement[] {
  const csvPath = join(levelDir.path, 'space.csv');
  if (!existsSync(csvPath)) return [];

  const csv = readCsv(csvPath);
  const color = COLORS.space ?? DEFAULT_COLOR;
  const results: CsvOnlyElement[] = [];

  for (const row of csv.rows) {
    const x = parseFloat(row.x ?? '');
    const y = parseFloat(row.y ?? '');
    if (isNaN(x) || isNaN(y)) continue;

    const name = row.name ?? row.id ?? '';
    const r = 0.15;
    let markup = `    <circle id="${row.id}" cx="${x.toFixed(3)}" cy="${y.toFixed(3)}" r="${r}" fill="${color.fill ?? 'none'}" stroke="${color.stroke}" stroke-width="0.03" />`;
    markup += `\n    <text x="${x.toFixed(3)}" y="${(-y).toFixed(3)}" font-size="0.4" fill="${color.stroke}" text-anchor="middle" dominant-baseline="central">${escapeXml(name)}</text>`;

    results.push({
      tableName: 'space',
      svgMarkup: markup,
      bounds: { minX: x - 1, minY: y - 1, maxX: x + 1, maxY: y + 1 },
    });
  }

  return results;
}

export function renderLevel(projectDir: string, levelId: string): string {
  const layout = discoverLayout(projectDir);
  const levelDir = layout.levelDirs.find((d) => d.name === levelId);
  if (!levelDir) {
    throw new Error(`Level "${levelId}" not found. Available: ${layout.levelDirs.map((d) => d.name).join(', ')}`);
  }

  // Collect all SVG-based elements with bounds
  const allElements: { tableName: string; el: SvgElement }[] = [];
  const csvOnlyElements: CsvOnlyElement[] = [];
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

  // Collect wall elements for hosted element position computation
  const wallElements = new Map<string, SvgElement>();

  for (const tableName of RENDER_ORDER) {
    if (CSV_ONLY_TABLES.has(tableName)) continue;

    const svgName = SVG_FILE_NAMES[tableName];
    if (!svgName) continue;
    const svgPath = join(levelDir.path, svgName + '.svg');
    if (!existsSync(svgPath)) continue;

    const svg = parseSvgFile(svgPath);
    for (const el of svg.elements) {
      allElements.push({ tableName, el });
      if (tableName === 'wall') wallElements.set(el.id, el);
      const b = elementBounds(el);
      if (b) {
        minX = Math.min(minX, b.minX);
        minY = Math.min(minY, b.minY);
        maxX = Math.max(maxX, b.maxX);
        maxY = Math.max(maxY, b.maxY);
      }
    }
  }

  // Render CSV-only elements
  for (const tableName of ['door', 'window'] as const) {
    csvOnlyElements.push(...renderHostedElements(levelDir, tableName, wallElements));
  }
  csvOnlyElements.push(...renderSpaces(levelDir));

  for (const el of csvOnlyElements) {
    minX = Math.min(minX, el.bounds.minX);
    minY = Math.min(minY, el.bounds.minY);
    maxX = Math.max(maxX, el.bounds.maxX);
    maxY = Math.max(maxY, el.bounds.maxY);
  }

  if (allElements.length === 0 && csvOnlyElements.length === 0) {
    return '<?xml version="1.0" encoding="utf-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">\n  <text x="5" y="5" text-anchor="middle" font-size="1">No geometry found</text>\n</svg>';
  }

  // viewBox: Y-flipped coordinates
  const pad = Math.max(maxX - minX, maxY - minY) * 0.05;
  const vbX = minX - pad;
  const vbY = -(maxY + pad);
  const vbW = (maxX - minX) + 2 * pad;
  const vbH = (maxY - minY) + 2 * pad;

  const parts: string[] = [
    '<?xml version="1.0" encoding="utf-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX.toFixed(3)} ${vbY.toFixed(3)} ${vbW.toFixed(3)} ${vbH.toFixed(3)}">`,
    `  <rect x="${vbX.toFixed(3)}" y="${vbY.toFixed(3)}" width="${vbW.toFixed(3)}" height="${vbH.toFixed(3)}" fill="white" />`,
    '  <g transform="scale(1,-1)">',
  ];

  // Render SVG-based elements in order
  for (const { tableName, el } of allElements) {
    const line = renderElement(el, tableName);
    if (line) parts.push(line);
  }

  // Render CSV-only elements
  for (const el of csvOnlyElements) {
    parts.push(el.svgMarkup);
  }

  parts.push('  </g>');
  parts.push('</svg>');
  return parts.join('\n');
}
