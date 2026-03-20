import { parseSvgFile } from '../utils/svg.js';

const ALLOWED_TAGS = new Set(['svg', 'g', 'line', 'rect', 'polygon', 'circle', 'text']);
const FORBIDDEN_TAGS = new Set(['path', 'defs', 'use', 'script', 'linearGradient', 'radialGradient', 'filter']);

export function validateSvgFile(
  displayPath: string,
  fullPath: string,
  csvIds: Set<string>,
  isHosted: boolean,
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

    // Hosted elements must have data-host
    if (isHosted && !el.attrs['data-host']) {
      issues.push(`${displayPath}  hosted element "${el.id}" missing data-host attribute`);
    }
  }

  return issues;
}
