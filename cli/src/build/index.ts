import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { validate } from '../validate/index.js';
import { discoverLayout } from '../utils/fs.js';
import { computeSpaceBoundaries } from './space-boundary.js';
import { validateGeometry } from './geometry-warnings.js';

export interface BuildResult {
  issues: string[];
  warnings: string[];
  artifacts: string[];
}

export function build(dir: string): BuildResult {
  // 1. Run all existing validation
  const issues = validate(dir);

  // 2. Geometry warnings (connectivity, hosted bounds, overlap)
  const warnings = validateGeometry(dir);

  // 3. Compute space boundaries per level → write space.svg
  const artifacts: string[] = [];
  const layout = discoverLayout(dir);

  for (const levelDir of layout.levelDirs) {
    const spaceCsvPath = join(levelDir.path, 'space.csv');
    if (!existsSync(spaceCsvPath)) continue;

    const result = computeSpaceBoundaries(levelDir);
    warnings.push(...result.warnings);
    if (result.svgWritten) {
      artifacts.push(`${levelDir.name}/space.svg`);
    }
  }

  return { issues, warnings, artifacts };
}
