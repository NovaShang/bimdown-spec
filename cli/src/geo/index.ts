/**
 * BimDown geometry toolkit — bridges SVG files and JSTS computational geometry.
 *
 * AI agents and scripts can use this to:
 *   - Load all geometry from a BimDown SVG file as JSTS geometries
 *   - Perform computational geometry operations (buffer, split, boolean ops)
 *   - Write results back to SVG files
 *
 * Example:
 *   import { readBimDownGeometry, writeBimDownGeometry, GeometryFactory, Coordinate } from 'bimdown-cli';
 *
 *   const slabs = readBimDownGeometry('project/lv-1/slab.svg');
 *   const slab = slabs.get('sl-1'); // JSTS Polygon
 *
 *   const factory = new GeometryFactory();
 *   const splitLine = factory.createPolygon(factory.createLinearRing([
 *     new Coordinate(-1000, -1000), new Coordinate(5, -1000),
 *     new Coordinate(5, 1000), new Coordinate(-1000, 1000),
 *     new Coordinate(-1000, -1000),
 *   ]));
 *   const left = slab.intersection(splitLine);
 *   const right = slab.difference(splitLine);
 *
 *   slabs.delete('sl-1');
 *   slabs.set('sl-1a', left);
 *   slabs.set('sl-1b', right);
 *   writeBimDownGeometry('project/lv-1/slab.svg', slabs);
 */
import { writeFileSync } from 'node:fs';
import { parseSvgFile, extractLineGeometry, extractRectGeometry, extractPolygonGeometry, extractCircleGeometry } from '../utils/svg.js';
import type { SvgElement } from '../utils/svg.js';
import { Coordinate, GeometryFactory } from './jsts-exports.js';

/**
 * Structural interface describing the subset of JSTS Geometry methods used by
 * this toolkit. We don't pull in real JSTS types (jsts has no .d.ts), but
 * consumers still get autocomplete for the common operations.
 */
export interface JstsGeometry {
  getGeometryType(): 'Point' | 'LineString' | 'Polygon' | 'MultiPoint' | 'MultiLineString' | 'MultiPolygon' | 'GeometryCollection' | (string & {});
  getCoordinates(): { x: number; y: number }[];
  getCoordinate(): { x: number; y: number };
  getEnvelopeInternal(): {
    getMinX(): number;
    getMinY(): number;
    getMaxX(): number;
    getMaxY(): number;
  };
  getNumGeometries(): number;
  getGeometryN(i: number): JstsGeometry;
  getExteriorRing(): JstsGeometry;
  intersection(other: JstsGeometry): JstsGeometry;
  union(other: JstsGeometry): JstsGeometry;
  difference(other: JstsGeometry): JstsGeometry;
  symDifference(other: JstsGeometry): JstsGeometry;
  buffer(distance: number): JstsGeometry;
  contains(other: JstsGeometry): boolean;
  intersects(other: JstsGeometry): boolean;
  isEmpty(): boolean;
}

export type BimDownGeometry = JstsGeometry;

export type GeometryMap = Map<string, BimDownGeometry>;

const factory = new GeometryFactory();

/**
 * Convert a single SVG element to a JSTS geometry.
 * Returns null if the element type is not supported.
 */
export function svgToJsts(el: SvgElement): BimDownGeometry | null {
  switch (el.tag) {
    case 'path': {
      // Line element — parse "M x,y L x,y" or "M x y L x y"
      const geo = extractLineGeometry(el);
      return factory.createLineString([
        new Coordinate(geo.start_x, geo.start_y),
        new Coordinate(geo.end_x, geo.end_y),
      ]);
    }
    case 'polygon': {
      const geo = extractPolygonGeometry(el);
      const pts = geo.points.trim().split(/\s+/).map((p) => {
        const [x, y] = p.split(',').map(Number);
        return new Coordinate(x ?? 0, y ?? 0);
      });
      // Close the ring if not already closed
      if (pts.length > 0) {
        const first = pts[0];
        const last = pts[pts.length - 1];
        if (first.x !== last.x || first.y !== last.y) {
          pts.push(new Coordinate(first.x, first.y));
        }
      }
      if (pts.length < 4) return null;
      return factory.createPolygon(factory.createLinearRing(pts));
    }
    case 'rect': {
      const x = parseFloat(el.attrs.x ?? '0');
      const y = parseFloat(el.attrs.y ?? '0');
      const w = parseFloat(el.attrs.width ?? '0');
      const h = parseFloat(el.attrs.height ?? '0');
      const pts = [
        new Coordinate(x, y),
        new Coordinate(x + w, y),
        new Coordinate(x + w, y + h),
        new Coordinate(x, y + h),
        new Coordinate(x, y),
      ];
      return factory.createPolygon(factory.createLinearRing(pts));
    }
    case 'circle': {
      const cx = parseFloat(el.attrs.cx ?? '0');
      const cy = parseFloat(el.attrs.cy ?? '0');
      return factory.createPoint(new Coordinate(cx, cy));
    }
    default:
      return null;
  }
}

/**
 * Convert a JSTS geometry back to an SVG element string (inner content only — no <svg> wrapper).
 * Determines the SVG tag from the geometry type:
 *   - LineString → <path d="M x,y L x,y" />
 *   - Polygon → <polygon points="x,y x,y ..." />
 *   - Point → <circle cx cy r="0.15" />  (default radius, caller may override attributes)
 *   - Multi* / GeometryCollection → emits multiple elements with id suffixes a, b, c, ...
 */
export function jstsToSvg(id: string, geom: BimDownGeometry, extraAttrs: Record<string, string> = {}): string {
  const attrs = { id, ...extraAttrs };
  const attrStr = (a: Record<string, string>) =>
    Object.entries(a).map(([k, v]) => `${k}="${v}"`).join(' ');

  const type = geom.getGeometryType();

  switch (type) {
    case 'LineString': {
      const coords = geom.getCoordinates();
      if (coords.length < 2) return '';
      const d = `M ${fmt(coords[0].x)},${fmt(coords[0].y)} L ${fmt(coords[coords.length - 1].x)},${fmt(coords[coords.length - 1].y)}`;
      return `<path ${attrStr({ ...attrs, d })} />`;
    }
    case 'Polygon': {
      const ring = geom.getExteriorRing();
      const coords = ring.getCoordinates();
      // Drop closing duplicate vertex
      const pts = coords.slice(0, coords.length - 1);
      const pointsStr = pts.map((c) => `${fmt(c.x)},${fmt(c.y)}`).join(' ');
      return `<polygon ${attrStr({ ...attrs, points: pointsStr })} />`;
    }
    case 'Point': {
      const c = geom.getCoordinate();
      const r = extraAttrs.r ?? '0.15';
      return `<circle ${attrStr({ ...attrs, cx: fmt(c.x), cy: fmt(c.y), r })} />`;
    }
  }

  // Multi* / GeometryCollection — unwrap into individual geometries.
  // 1 sub-geometry → recurse with the same id.
  // N > 1 → suffix ids with a, b, c, ...
  const n = typeof geom.getNumGeometries === 'function' ? geom.getNumGeometries() : 0;
  if (n === 1) {
    return jstsToSvg(id, geom.getGeometryN(0), extraAttrs);
  }
  if (n > 1) {
    const parts: string[] = [];
    for (let i = 0; i < n; i++) {
      const suffix = String.fromCharCode(97 + i); // a, b, c, ...
      parts.push(jstsToSvg(`${id}${suffix}`, geom.getGeometryN(i), extraAttrs));
    }
    return parts.filter(Boolean).join('\n    ');
  }

  return '';
}

/**
 * Read an entire BimDown SVG file and return a Map of element id → JSTS geometry.
 *
 * All geometries are in the same Cartesian coordinate system as the SVG
 * (after the `<g transform="scale(1,-1)">` flip — i.e. Y-up meters).
 */
export function readBimDownGeometry(filePath: string): GeometryMap {
  const svg = parseSvgFile(filePath);
  const map: GeometryMap = new Map();
  for (const el of svg.elements) {
    if (!el.id) continue;
    const geom = svgToJsts(el);
    if (geom) map.set(el.id, geom);
  }
  return map;
}

/**
 * Write a GeometryMap back to an SVG file. Computes a tight viewBox from the
 * geometries. Preserves the standard BimDown `<g transform="scale(1,-1)">`
 * wrapper and Y-up coordinate convention.
 */
export function writeBimDownGeometry(filePath: string, geometries: GeometryMap): void {
  const innerElements: string[] = [];
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

  for (const [id, geom] of geometries) {
    const svgEl = jstsToSvg(id, geom);
    if (svgEl) innerElements.push(svgEl);

    const env = geom.getEnvelopeInternal();
    minX = Math.min(minX, env.getMinX());
    minY = Math.min(minY, env.getMinY());
    maxX = Math.max(maxX, env.getMaxX());
    maxY = Math.max(maxY, env.getMaxY());
  }

  const pad = Math.max(maxX - minX, maxY - minY) * 0.02;
  const vbX = isFinite(minX) ? minX - pad : 0;
  const vbY = isFinite(minY) ? -(maxY + pad) : 0;
  const vbW = isFinite(maxX) ? (maxX - minX) + pad * 2 : 100;
  const vbH = isFinite(maxY) ? (maxY - minY) + pad * 2 : 100;

  const svg = [
    '<?xml version="1.0" encoding="utf-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${fmt(vbX)} ${fmt(vbY)} ${fmt(vbW)} ${fmt(vbH)}">`,
    '  <g transform="scale(1,-1)">',
    ...innerElements.map((e) => '    ' + e),
    '  </g>',
    '</svg>',
  ].join('\n');

  writeFileSync(filePath, svg, 'utf-8');
}

function fmt(n: number): string {
  if (!isFinite(n)) return '0';
  return Number(n.toFixed(3)).toString();
}
