import { parseSvgFile } from '../utils/svg.js';

const ALLOWED_TAGS = new Set(['svg', 'g', 'path', 'rect', 'polygon', 'circle', 'text']);
const FORBIDDEN_TAGS = new Set(['line', 'defs', 'use', 'script', 'linearGradient', 'radialGradient', 'filter']);

/** Expected SVG element tag(s) per table type */
const EXPECTED_TAGS: Record<string, Set<string>> = {
  // Line elements → <path>
  wall: new Set(['path']),
  structure_wall: new Set(['path']),
  curtain_wall: new Set(['path']),
  room_separator: new Set(['path']),
  stair: new Set(['path']),
  ramp: new Set(['path']),
  railing: new Set(['path']),
  beam: new Set(['path']),
  brace: new Set(['path']),
  duct: new Set(['path']),
  pipe: new Set(['path']),
  cable_tray: new Set(['path']),
  conduit: new Set(['path']),
  // Point elements → <rect> | <circle>
  column: new Set(['rect', 'circle']),
  structure_column: new Set(['rect', 'circle']),
  equipment: new Set(['rect', 'circle']),
  terminal: new Set(['rect', 'circle']),
  mep_node: new Set(['rect', 'circle']),
  // Polygon elements → <polygon>
  slab: new Set(['polygon']),
  roof: new Set(['polygon']),
  ceiling: new Set(['polygon']),
  structure_slab: new Set(['polygon']),
  // Mixed geometry
  foundation: new Set(['rect', 'circle', 'path', 'polygon']),
  opening: new Set(['rect', 'polygon']),
};

/** Parse path d attribute and extract all numeric coordinates */
function extractPathCoords(d: string): number[] {
  const coords: number[] = [];
  // Match all numbers (including negative and decimal) in the d attribute
  const nums = d.match(/-?[\d.]+/g);
  if (nums) {
    for (const n of nums) {
      const val = parseFloat(n);
      if (!isNaN(val)) coords.push(val);
    }
  }
  return coords;
}

export function validateSvgFile(
  displayPath: string,
  fullPath: string,
  csvIds: Set<string>,
  isHosted: boolean,
  tableName?: string,
): string[] {
  const issues: string[] = [];

  let svg: ReturnType<typeof parseSvgFile>;
  try {
    svg = parseSvgFile(fullPath);
  } catch (e) {
    issues.push(`${displayPath}  failed to parse SVG: ${(e as Error).message}`);
    return issues;
  }

  // Check Y-axis flip
  if (!svg.hasYFlip) {
    issues.push(`${displayPath}  missing required <g transform="scale(1,-1)"> for Y-axis flip`);
  }

  // Check for forbidden tags
  for (const tag of svg.allTags) {
    if (FORBIDDEN_TAGS.has(tag)) {
      issues.push(`${displayPath}  forbidden tag <${tag}> found`);
    }
    // Warn about unknown tags (not in allowed or forbidden)
    if (!ALLOWED_TAGS.has(tag) && !FORBIDDEN_TAGS.has(tag) && !tag.startsWith('?')) {
      issues.push(`${displayPath}  unknown tag <${tag}>`);
    }
  }

  // Check element IDs match CSV
  const svgIds = new Set<string>();
  for (const el of svg.elements) {
    if (svgIds.has(el.id)) {
      issues.push(`${displayPath}  duplicate SVG element id "${el.id}"`);
    }
    svgIds.add(el.id);

    if (!csvIds.has(el.id)) {
      issues.push(`${displayPath}  SVG element "${el.id}" has no matching CSV row`);
    }

    // Check element uses correct SVG tag for this table type
    if (tableName && EXPECTED_TAGS[tableName] && !EXPECTED_TAGS[tableName].has(el.tag)) {
      const expected = [...EXPECTED_TAGS[tableName]].join(' or ');
      issues.push(`${displayPath}  element "${el.id}" uses <${el.tag}> but ${tableName} requires <${expected}>`);
    }

    // Hosted elements must have data-host
    if (isHosted && !el.attrs['data-host']) {
      issues.push(`${displayPath}  hosted element "${el.id}" missing data-host attribute`);
    }

    // Check coordinate ranges for <path> elements
    if (el.tag === 'path' && el.attrs.d) {
      const coords = extractPathCoords(el.attrs.d);
      for (const val of coords) {
        if (Math.abs(val) > 1000) {
          issues.push(
            `${displayPath}  element "${el.id}" path coordinates look like millimeters — SVG coordinates must be in meters`,
          );
          break; // one warning per element
        }
      }
    }

    // Check coordinate ranges for non-path elements
    const coordAttrs = ['x', 'y', 'cx', 'cy', 'r', 'width', 'height'];
    for (const attr of coordAttrs) {
      const raw = el.attrs[attr];
      if (raw === undefined) continue;
      const val = Number(raw);
      if (!isNaN(val) && Math.abs(val) > 1000) {
        issues.push(
          `${displayPath}  element "${el.id}" ${attr}=${raw} looks like millimeters — SVG coordinates must be in meters`,
        );
      }
    }

    // Check polygon points range
    if (el.attrs['points']) {
      const points = el.attrs['points'].trim().split(/\s+/);
      for (const pt of points) {
        const [px, py] = pt.split(',').map(Number);
        if ((!isNaN(px) && Math.abs(px) > 1000) || (!isNaN(py) && Math.abs(py) > 1000)) {
          issues.push(
            `${displayPath}  element "${el.id}" polygon point ${pt} looks like millimeters — coordinates must be in meters`,
          );
          break; // one warning per element is enough
        }
      }
    }
  }

  return issues;
}
