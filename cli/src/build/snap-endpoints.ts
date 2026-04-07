import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseSvgFile, extractLineGeometry } from '../utils/svg.js';
import { discoverLayout, listFiles } from '../utils/fs.js';

const SNAP_TOLERANCE = 0.05; // 5cm

const BOUNDARY_TABLES = ['wall', 'structure_wall', 'curtain_wall', 'room_separator'];

interface Point {
  x: number;
  y: number;
}

interface EndpointRef {
  point: Point;
  table: string;
  elementId: string;
  side: 'start' | 'end';
  levelDir: string;
  levelPath: string;
}

/**
 * Pre-build step: snap wall endpoints that are within SNAP_TOLERANCE of each other.
 * Modifies SVG files in-place. Returns count of snapped endpoints.
 */
export function snapEndpoints(dir: string): number {
  const layout = discoverLayout(dir);
  const allDirs = [
    { name: 'global', path: layout.globalDir },
    ...layout.levelDirs,
  ];

  let totalSnapped = 0;

  // Process each level independently (endpoints only snap within same level)
  for (const d of allDirs) {
    if (!existsSync(d.path)) continue;

    // Collect all endpoints in this level
    const endpoints: EndpointRef[] = [];
    for (const table of BOUNDARY_TABLES) {
      const svgPath = join(d.path, `${table}.svg`);
      if (!existsSync(svgPath)) continue;
      try {
        const svg = parseSvgFile(svgPath);
        for (const el of svg.elements) {
          if (el.tag !== 'path') continue;
          const geo = extractLineGeometry(el);
          endpoints.push({ point: { x: geo.start_x, y: geo.start_y }, table, elementId: el.id, side: 'start', levelDir: d.name, levelPath: d.path });
          endpoints.push({ point: { x: geo.end_x, y: geo.end_y }, table, elementId: el.id, side: 'end', levelDir: d.name, levelPath: d.path });
        }
      } catch { /* skip */ }
    }

    if (endpoints.length === 0) continue;

    // Build clusters of nearby endpoints
    const clusters = clusterEndpoints(endpoints);

    // For each cluster with > 1 endpoint, snap to canonical position
    const snapMap = new Map<string, Point>(); // "table:elementId:side" → snapped point
    for (const cluster of clusters) {
      if (cluster.length <= 1) continue;

      // Canonical = the point that already has the most connections (most common coordinate)
      const canonical = pickCanonical(cluster);

      for (const ep of cluster) {
        const dx = Math.abs(ep.point.x - canonical.x);
        const dy = Math.abs(ep.point.y - canonical.y);
        if (dx > 1e-6 || dy > 1e-6) {
          const key = `${ep.table}:${ep.elementId}:${ep.side}`;
          snapMap.set(key, canonical);
          totalSnapped++;
        }
      }
    }

    if (snapMap.size === 0) continue;

    // Apply snaps to SVG files
    for (const table of BOUNDARY_TABLES) {
      const svgPath = join(d.path, `${table}.svg`);
      if (!existsSync(svgPath)) continue;

      let svgText = readFileSync(svgPath, 'utf-8');
      let modified = false;

      // Re-parse to get element geometries
      try {
        const svg = parseSvgFile(svgPath);
        for (const el of svg.elements) {
          if (el.tag !== 'path') continue;
          const geo = extractLineGeometry(el);

          const startKey = `${table}:${el.id}:start`;
          const endKey = `${table}:${el.id}:end`;
          const newStart = snapMap.get(startKey);
          const newEnd = snapMap.get(endKey);

          if (!newStart && !newEnd) continue;

          const sx = newStart?.x ?? geo.start_x;
          const sy = newStart?.y ?? geo.start_y;
          const ex = newEnd?.x ?? geo.end_x;
          const ey = newEnd?.y ?? geo.end_y;

          // Replace the d attribute for this element
          const oldD = el.attrs.d;
          const newD = `M ${fmtNum(sx)} ${fmtNum(sy)} L ${fmtNum(ex)} ${fmtNum(ey)}`;

          if (oldD && oldD !== newD) {
            // Use a targeted replacement: find this element's path and replace its d attribute
            svgText = replacePathD(svgText, el.id, oldD, newD);
            modified = true;
          }
        }
      } catch { continue; }

      if (modified) {
        writeFileSync(svgPath, svgText, 'utf-8');
      }
    }
  }

  return totalSnapped;
}

function clusterEndpoints(endpoints: EndpointRef[]): EndpointRef[][] {
  const visited = new Set<number>();
  const clusters: EndpointRef[][] = [];

  for (let i = 0; i < endpoints.length; i++) {
    if (visited.has(i)) continue;
    const cluster: EndpointRef[] = [endpoints[i]];
    visited.add(i);

    // Find all endpoints within tolerance of any member of this cluster
    let expanded = true;
    while (expanded) {
      expanded = false;
      for (let j = 0; j < endpoints.length; j++) {
        if (visited.has(j)) continue;
        for (const member of cluster) {
          if (dist(member.point, endpoints[j].point) < SNAP_TOLERANCE) {
            cluster.push(endpoints[j]);
            visited.add(j);
            expanded = true;
            break;
          }
        }
      }
    }

    clusters.push(cluster);
  }

  return clusters;
}

function pickCanonical(cluster: EndpointRef[]): Point {
  // Count how many times each coordinate appears (exact match)
  const counts = new Map<string, { point: Point; count: number }>();
  for (const ep of cluster) {
    const key = `${fmtNum(ep.point.x)},${fmtNum(ep.point.y)}`;
    const entry = counts.get(key);
    if (entry) {
      entry.count++;
    } else {
      counts.set(key, { point: ep.point, count: 1 });
    }
  }

  // Pick the most common point
  let best: { point: Point; count: number } = { point: cluster[0].point, count: 0 };
  for (const entry of counts.values()) {
    if (entry.count > best.count) best = entry;
  }
  return best.point;
}

function dist(a: Point, b: Point): number {
  return Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2);
}

function fmtNum(n: number): string {
  // Use reasonable precision, trim trailing zeros
  const s = n.toFixed(4);
  return s.replace(/\.?0+$/, '') || '0';
}

function replacePathD(svgText: string, id: string, oldD: string, newD: string): string {
  // Find the <path> element with this id and replace its d attribute
  // Match pattern: id="<id>" ... d="<oldD>" or d="<oldD>" ... id="<id>"
  const escaped = oldD.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const idEscaped = id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  // Try to find and replace the d attribute within the path element that has this id
  const pathRegex = new RegExp(`(<path[^>]*\\bid="${idEscaped}"[^>]*\\bd=")${escaped}(")`);
  if (pathRegex.test(svgText)) {
    return svgText.replace(pathRegex, `$1${newD}$2`);
  }

  // Also try d before id
  const pathRegex2 = new RegExp(`(<path[^>]*\\bd=")${escaped}("[^>]*\\bid="${idEscaped}")`);
  if (pathRegex2.test(svgText)) {
    return svgText.replace(pathRegex2, `$1${newD}$2`);
  }

  return svgText;
}
