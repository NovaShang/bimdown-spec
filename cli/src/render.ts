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
  'space',
  'wall', 'structure_wall',
  'column', 'structure_column',
  'beam', 'brace',
  'stair',
  'duct', 'pipe', 'cable_tray', 'conduit',
  'equipment', 'terminal',
  'door', 'window',
];

function elementBounds(el: SvgElement): { minX: number; minY: number; maxX: number; maxY: number } | null {
  switch (el.tag) {
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

function renderElement(el: SvgElement, tableName: string, spaceName?: string): string {
  const color = COLORS[tableName] ?? DEFAULT_COLOR;

  switch (el.tag) {
    case 'line': {
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      const sw = el.attrs['stroke-width'] ?? '0.2';
      return `    <line ${attrs} stroke="${color.stroke}" stroke-width="${sw}" stroke-linecap="square" />`;
    }
    case 'polygon': {
      const fill = color.fill ?? 'none';
      const attrs = Object.entries(el.attrs)
        .filter(([k]) => !OVERRIDE_ATTRS.has(k))
        .map(([k, v]) => `${k}="${v}"`)
        .join(' ');
      let result = `    <polygon ${attrs} fill="${fill}" stroke="${color.stroke}" stroke-width="0.05" />`;
      if (spaceName) {
        const b = elementBounds(el);
        if (b) {
          const cx = (b.minX + b.maxX) / 2;
          const cy = (b.minY + b.maxY) / 2;
          const fontSize = Math.min(b.maxX - b.minX, b.maxY - b.minY) * 0.12;
          result += `\n    <text x="${cx}" y="${-cy}" font-size="${Math.max(fontSize, 0.3).toFixed(2)}" fill="${color.stroke}" text-anchor="middle" dominant-baseline="central">${escapeXml(spaceName)}</text>`;
        }
      }
      return result;
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

export function renderLevel(projectDir: string, levelId: string): string {
  const layout = discoverLayout(projectDir);
  const levelDir = layout.levelDirs.find((d) => d.name === levelId);
  if (!levelDir) {
    throw new Error(`Level "${levelId}" not found. Available: ${layout.levelDirs.map((d) => d.name).join(', ')}`);
  }

  // Load space names from CSV for labeling
  const spaceNames = new Map<string, string>();
  const spaceCsvPath = join(levelDir.path, 'space.csv');
  if (existsSync(spaceCsvPath)) {
    const csv = readCsv(spaceCsvPath);
    for (const row of csv.rows) {
      if (row.id && row.name) spaceNames.set(row.id, row.name);
    }
  }

  // Collect all elements with bounds
  const allElements: { tableName: string; el: SvgElement; spaceName?: string }[] = [];
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

  for (const tableName of RENDER_ORDER) {
    const svgName = SVG_FILE_NAMES[tableName];
    if (!svgName) continue;
    const svgPath = join(levelDir.path, svgName + '.svg');
    if (!existsSync(svgPath)) continue;

    const svg = parseSvgFile(svgPath);
    for (const el of svg.elements) {
      allElements.push({
        tableName,
        el,
        spaceName: tableName === 'space' ? spaceNames.get(el.id) : undefined,
      });
      const b = elementBounds(el);
      if (b) {
        minX = Math.min(minX, b.minX);
        minY = Math.min(minY, b.minY);
        maxX = Math.max(maxX, b.maxX);
        maxY = Math.max(maxY, b.maxY);
      }
    }
  }

  if (allElements.length === 0) {
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

  for (const { tableName, el, spaceName } of allElements) {
    const line = renderElement(el, tableName, spaceName);
    if (line) parts.push(line);
  }

  parts.push('  </g>');
  parts.push('</svg>');
  return parts.join('\n');
}
