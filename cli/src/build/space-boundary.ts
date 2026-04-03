import { existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseSvgFile, extractLineGeometry } from '../utils/svg.js';
import { readCsv } from '../utils/csv.js';

const TOLERANCE = 0.01; // 1cm in meters
const MAX_FACE_EDGES = 10000; // safety limit for face tracing

/** Boundary element SVG file names to collect line segments from */
const BOUNDARY_TABLES = ['wall', 'structure_wall', 'curtain_wall', 'room_separator'];

// ─── Data structures ────────────────────────────────────

interface Vertex {
  x: number;
  y: number;
  id: number;
  outgoing: HalfEdge[];
}

interface HalfEdge {
  from: Vertex;
  to: Vertex;
  twin: HalfEdge;
  next: HalfEdge;
  visited: boolean;
  sourceId: string;
}

interface Face {
  polygon: { x: number; y: number }[];
  signedArea: number;
}

interface Segment {
  startX: number;
  startY: number;
  endX: number;
  endY: number;
  id: string;
}

export interface SpaceBoundaryResult {
  warnings: string[];
  svgWritten: boolean;
}

// ─── Main entry point ───────────────────────────────────

export function computeSpaceBoundaries(
  levelDir: { name: string; path: string },
): SpaceBoundaryResult {
  const warnings: string[] = [];

  const spaceCsvPath = join(levelDir.path, 'space.csv');
  if (!existsSync(spaceCsvPath)) return { warnings, svgWritten: false };

  const spaceCsv = readCsv(spaceCsvPath);
  if (spaceCsv.rows.length === 0) return { warnings, svgWritten: false };

  // 1. Collect all boundary line segments
  const segments = collectBoundarySegments(levelDir.path);
  if (segments.length === 0) {
    warnings.push(`${levelDir.name}/  no boundary elements found for space boundary computation`);
    return { warnings, svgWritten: false };
  }

  // 2-5. Build half-edge structure
  const { vertices, halfEdges } = buildHalfEdgeStructure(segments);

  // Report dangling endpoints (degree 1 vertices)
  for (const v of vertices) {
    if (v.outgoing.length === 1) {
      const he = v.outgoing[0];
      warnings.push(
        `[warn] ${levelDir.name}/  ${he.sourceId} endpoint (${v.x.toFixed(3)}, ${v.y.toFixed(3)}) has no connected line element`,
      );
    }
  }

  // 6. Trace faces
  const faces = traceFaces(halfEdges);

  // 7. Match seed points to faces
  const matchedSpaces = new Map<string, Face>();
  for (const row of spaceCsv.rows) {
    const seedX = parseFloat(row.x ?? '');
    const seedY = parseFloat(row.y ?? '');
    if (isNaN(seedX) || isNaN(seedY)) continue;

    let bestFace: Face | null = null;
    let bestArea = Infinity;

    for (const face of faces) {
      // Skip outer boundary face (CW winding = negative signed area)
      if (face.signedArea <= 0) continue;
      if (face.signedArea < bestArea && pointInPolygon(seedX, seedY, face.polygon)) {
        bestFace = face;
        bestArea = face.signedArea;
      }
    }

    if (bestFace) {
      matchedSpaces.set(row.id, bestFace);
    } else {
      warnings.push(
        `[warn] ${levelDir.name}/space.csv  ${row.id} at (${row.x}, ${row.y}) has no enclosing boundary`,
      );
    }
  }

  if (matchedSpaces.size === 0) return { warnings, svgWritten: false };

  // 8. Write space.svg
  writeSvg(levelDir.path, matchedSpaces);
  return { warnings, svgWritten: true };
}

// ─── Segment collection ─────────────────────────────────

function collectBoundarySegments(levelPath: string): Segment[] {
  const segments: Segment[] = [];

  for (const table of BOUNDARY_TABLES) {
    const svgPath = join(levelPath, `${table}.svg`);
    if (!existsSync(svgPath)) continue;

    try {
      const svg = parseSvgFile(svgPath);
      for (const el of svg.elements) {
        if (el.tag !== 'path') continue;
        const geo = extractLineGeometry(el);
        if (geo.length < TOLERANCE) continue; // skip degenerate segments
        segments.push({
          startX: geo.start_x,
          startY: geo.start_y,
          endX: geo.end_x,
          endY: geo.end_y,
          id: el.id,
        });
      }
    } catch {
      // Skip unparseable SVGs
    }
  }

  return segments;
}

// ─── Half-edge construction ─────────────────────────────

function buildHalfEdgeStructure(segments: Segment[]): {
  vertices: Vertex[];
  halfEdges: HalfEdge[];
} {
  const vertices: Vertex[] = [];

  function findOrCreateVertex(x: number, y: number): Vertex {
    for (const v of vertices) {
      if (Math.abs(v.x - x) < TOLERANCE && Math.abs(v.y - y) < TOLERANCE) {
        return v;
      }
    }
    const v: Vertex = { x, y, id: vertices.length, outgoing: [] };
    vertices.push(v);
    return v;
  }

  // Create half-edges for each segment
  const halfEdges: HalfEdge[] = [];

  for (const seg of segments) {
    const v1 = findOrCreateVertex(seg.startX, seg.startY);
    const v2 = findOrCreateVertex(seg.endX, seg.endY);
    if (v1 === v2) continue; // degenerate

    // Check for duplicate edge (same two vertices already connected)
    const isDuplicate = v1.outgoing.some((he) => he.to === v2);
    if (isDuplicate) continue;

    const he1 = { from: v1, to: v2, sourceId: seg.id } as HalfEdge;
    const he2 = { from: v2, to: v1, sourceId: seg.id } as HalfEdge;
    he1.twin = he2;
    he2.twin = he1;
    he1.visited = false;
    he2.visited = false;

    v1.outgoing.push(he1);
    v2.outgoing.push(he2);
    halfEdges.push(he1, he2);
  }

  // Sort outgoing edges at each vertex by angle
  for (const v of vertices) {
    v.outgoing.sort((a, b) => {
      const angleA = Math.atan2(a.to.y - v.y, a.to.x - v.x);
      const angleB = Math.atan2(b.to.y - v.y, b.to.x - v.x);
      return angleA - angleB;
    });
  }

  // Link next pointers:
  // For half-edge `he` arriving at vertex v, the face continues with
  // the outgoing edge one step CW from the twin (which leaves v going back).
  for (const he of halfEdges) {
    const v = he.to;
    const twinIdx = v.outgoing.indexOf(he.twin);
    if (twinIdx === -1) {
      // Should not happen, but handle gracefully
      he.next = he.twin;
      continue;
    }
    const prevIdx = (twinIdx - 1 + v.outgoing.length) % v.outgoing.length;
    he.next = v.outgoing[prevIdx];
  }

  return { vertices, halfEdges };
}

// ─── Face tracing ───────────────────────────────────────

function traceFaces(halfEdges: HalfEdge[]): Face[] {
  const faces: Face[] = [];

  for (const startHe of halfEdges) {
    if (startHe.visited) continue;

    const polygon: { x: number; y: number }[] = [];
    let current = startHe;
    let count = 0;

    do {
      current.visited = true;
      polygon.push({ x: current.from.x, y: current.from.y });
      current = current.next;
      count++;
    } while (current !== startHe && count < MAX_FACE_EDGES);

    if (count >= MAX_FACE_EDGES) continue; // malformed cycle, skip

    // Compute signed area (shoelace formula)
    // Positive = CCW (interior face), Negative = CW (exterior/boundary face)
    let signedArea = 0;
    for (let i = 0; i < polygon.length; i++) {
      const j = (i + 1) % polygon.length;
      signedArea += polygon[i].x * polygon[j].y;
      signedArea -= polygon[j].x * polygon[i].y;
    }
    signedArea /= 2;

    faces.push({ polygon, signedArea });
  }

  return faces;
}

// ─── Point-in-polygon (ray casting) ─────────────────────

function pointInPolygon(px: number, py: number, polygon: { x: number; y: number }[]): boolean {
  let crossings = 0;
  for (let i = 0; i < polygon.length; i++) {
    const j = (i + 1) % polygon.length;
    const yi = polygon[i].y;
    const yj = polygon[j].y;
    const xi = polygon[i].x;
    const xj = polygon[j].x;

    if ((yi <= py && py < yj) || (yj <= py && py < yi)) {
      const t = (py - yi) / (yj - yi);
      const xIntersect = xi + t * (xj - xi);
      if (px < xIntersect) crossings++;
    }
  }
  return crossings % 2 === 1;
}

// ─── SVG output ─────────────────────────────────────────

function writeSvg(levelPath: string, matchedSpaces: Map<string, Face>): void {
  // Compute viewBox from all polygon bounds
  let minX = Infinity,
    minY = Infinity,
    maxX = -Infinity,
    maxY = -Infinity;

  for (const face of matchedSpaces.values()) {
    for (const p of face.polygon) {
      minX = Math.min(minX, p.x);
      minY = Math.min(minY, p.y);
      maxX = Math.max(maxX, p.x);
      maxY = Math.max(maxY, p.y);
    }
  }

  const pad = Math.max(maxX - minX, maxY - minY) * 0.02;
  const vbX = (minX - pad).toFixed(3);
  const vbY = (-(maxY + pad)).toFixed(3);
  const vbW = (maxX - minX + pad * 2).toFixed(3);
  const vbH = (maxY - minY + pad * 2).toFixed(3);

  const lines: string[] = [
    '<?xml version="1.0" encoding="utf-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX} ${vbY} ${vbW} ${vbH}">`,
    '  <g transform="scale(1,-1)">',
  ];

  for (const [spaceId, face] of matchedSpaces) {
    const pointsStr = face.polygon
      .map((p) => `${p.x.toFixed(3)},${p.y.toFixed(3)}`)
      .join(' ');
    lines.push(`    <polygon id="${spaceId}" points="${pointsStr}" />`);
  }

  lines.push('  </g>');
  lines.push('</svg>');

  writeFileSync(join(levelPath, 'space.svg'), lines.join('\n'));
}
