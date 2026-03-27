import { readFileSync } from 'node:fs';
import { XMLParser } from 'fast-xml-parser';

export interface SvgElement {
  tag: string;
  id: string;
  attrs: Record<string, string>;
}

export interface SvgFile {
  elements: SvgElement[];
  allTags: string[];
  hasYFlip: boolean;
  raw: unknown;
}

const parser = new XMLParser({
  ignoreAttributes: false,
  attributeNamePrefix: '@_',
  allowBooleanAttributes: true,
  parseAttributeValue: false,
  trimValues: true,
});

export function parseSvgFile(filePath: string): SvgFile {
  let text = readFileSync(filePath, 'utf-8');
  if (text.charCodeAt(0) === 0xfeff) text = text.slice(1);

  const parsed = parser.parse(text);
  const elements: SvgElement[] = [];
  const allTags: string[] = [];
  let hasYFlip = false;

  function walk(node: any, tag?: string) {
    if (!node || typeof node !== 'object') return;

    if (tag) allTags.push(tag);

    // Check for Y-axis flip
    if (tag === 'g') {
      const transform = node['@_transform'] ?? '';
      if (typeof transform === 'string' && transform.includes('scale(1,-1)')) {
        hasYFlip = true;
      }
    }

    // Extract geometry elements
    const geoTags = ['line', 'rect', 'polygon', 'circle', 'text'];
    if (tag && geoTags.includes(tag)) {
      const attrs: Record<string, string> = {};
      for (const [k, v] of Object.entries(node)) {
        if (k.startsWith('@_')) {
          attrs[k.slice(2)] = String(v);
        }
      }
      if (attrs.id) {
        elements.push({ tag, id: attrs.id, attrs });
      }
    }

    // Recurse into children
    for (const [key, value] of Object.entries(node)) {
      if (key.startsWith('@_') || key.startsWith('?')) continue;
      if (Array.isArray(value)) {
        for (const item of value) walk(item, key);
      } else if (typeof value === 'object' && value !== null) {
        walk(value, key);
      }
    }
  }

  walk(parsed);
  return { elements, allTags, hasYFlip, raw: parsed };
}

export interface LineGeometry {
  start_x: number;
  start_y: number;
  end_x: number;
  end_y: number;
  length: number;
}

export interface RectGeometry {
  x: number;
  y: number;
  rotation: number;
  size_x: number;
  size_y: number;
}

export interface PolygonGeometry {
  points: string;
  area: number;
}

export interface CircleGeometry {
  x: number;
  y: number;
  size_x: number;
  size_y: number;
}

export function extractLineGeometry(el: SvgElement): LineGeometry {
  const x1 = parseFloat(el.attrs.x1 ?? '0');
  const y1 = parseFloat(el.attrs.y1 ?? '0');
  const x2 = parseFloat(el.attrs.x2 ?? '0');
  const y2 = parseFloat(el.attrs.y2 ?? '0');
  const dx = x2 - x1;
  const dy = y2 - y1;
  return {
    start_x: x1, start_y: y1,
    end_x: x2, end_y: y2,
    length: Math.sqrt(dx * dx + dy * dy),
  };
}

export function extractRectGeometry(el: SvgElement): RectGeometry {
  const x = parseFloat(el.attrs.x ?? '0');
  const y = parseFloat(el.attrs.y ?? '0');
  const w = parseFloat(el.attrs.width ?? '0');
  const h = parseFloat(el.attrs.height ?? '0');
  let rotation = 0;
  const transform = el.attrs.transform;
  if (transform) {
    const m = transform.match(/rotate\(([^,)]+)/);
    if (m) rotation = parseFloat(m[1]);
  }
  return {
    x: x + w / 2,
    y: y + h / 2,
    rotation,
    size_x: w,
    size_y: h,
  };
}

export function extractPolygonGeometry(el: SvgElement): PolygonGeometry {
  const pointsStr = el.attrs.points ?? '';
  const coords = pointsStr.trim().split(/\s+/).map((p) => {
    const [x, y] = p.split(',').map(Number);
    return { x: x ?? 0, y: y ?? 0 };
  });
  // Shoelace formula for area
  let area = 0;
  for (let i = 0; i < coords.length; i++) {
    const j = (i + 1) % coords.length;
    area += coords[i].x * coords[j].y;
    area -= coords[j].x * coords[i].y;
  }
  return { points: pointsStr, area: Math.abs(area) / 2 };
}

export function extractCircleGeometry(el: SvgElement): CircleGeometry {
  const cx = parseFloat(el.attrs.cx ?? '0');
  const cy = parseFloat(el.attrs.cy ?? '0');
  const r = parseFloat(el.attrs.r ?? '0');
  return { x: cx, y: cy, size_x: r * 2, size_y: r * 2 };
}

// ─── SVG writing / merging ───────────────────────────────

function formatSvgAttrs(attrs: Record<string, string>, idOverride?: string): string {
  const parts: string[] = [];
  for (const [k, v] of Object.entries(attrs)) {
    const val = k === 'id' && idOverride ? idOverride : v;
    parts.push(`${k}="${val}"`);
  }
  return parts.join(' ');
}

function elementBounds(el: SvgElement): { minX: number; minY: number; maxX: number; maxY: number } {
  switch (el.tag) {
    case 'line': {
      const x1 = parseFloat(el.attrs.x1 ?? '0'), y1 = parseFloat(el.attrs.y1 ?? '0');
      const x2 = parseFloat(el.attrs.x2 ?? '0'), y2 = parseFloat(el.attrs.y2 ?? '0');
      return { minX: Math.min(x1, x2), minY: Math.min(y1, y2), maxX: Math.max(x1, x2), maxY: Math.max(y1, y2) };
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
      if (pts.length === 0) return { minX: 0, minY: 0, maxX: 0, maxY: 0 };
      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
      for (const p of pts) {
        minX = Math.min(minX, p.x); minY = Math.min(minY, p.y);
        maxX = Math.max(maxX, p.x); maxY = Math.max(maxY, p.y);
      }
      return { minX, minY, maxX, maxY };
    }
    default:
      return { minX: 0, minY: 0, maxX: 0, maxY: 0 };
  }
}

export interface MergeSvgInput {
  elements: SvgElement[];
  idRemap: Map<string, string>;
}

export function writeMergedSvg(inputs: MergeSvgInput[]): string {
  const allElements: { el: SvgElement; newId: string }[] = [];

  for (const input of inputs) {
    for (const el of input.elements) {
      const newId = input.idRemap.get(el.id) ?? el.id;
      allElements.push({ el, newId });
    }
  }

  if (allElements.length === 0) {
    return '<?xml version="1.0" encoding="utf-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 0 0">\n  <g transform="scale(1,-1)">\n  </g>\n</svg>';
  }

  // Compute union viewBox from element bounds (coordinates are in flipped space)
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const { el } of allElements) {
    const b = elementBounds(el);
    minX = Math.min(minX, b.minX); minY = Math.min(minY, b.minY);
    maxX = Math.max(maxX, b.maxX); maxY = Math.max(maxY, b.maxY);
  }

  // viewBox uses positive-Y-up after scale(1,-1), so negate Y bounds
  const vbX = minX;
  const vbY = -maxY;
  const vbW = maxX - minX;
  const vbH = maxY - minY;

  const pad = Math.max(vbW, vbH) * 0.02;
  const viewBox = `${(vbX - pad).toFixed(3)} ${(vbY - pad).toFixed(3)} ${(vbW + pad * 2).toFixed(3)} ${(vbH + pad * 2).toFixed(3)}`;

  const lines: string[] = [
    '<?xml version="1.0" encoding="utf-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}">`,
    '  <g transform="scale(1,-1)">',
  ];

  for (const { el, newId } of allElements) {
    lines.push(`    <${el.tag} ${formatSvgAttrs(el.attrs, newId)} />`);
  }

  lines.push('  </g>');
  lines.push('</svg>');
  return lines.join('\n');
}
