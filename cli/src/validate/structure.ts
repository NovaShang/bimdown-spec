import { existsSync } from 'node:fs';
import { join, extname } from 'node:path';
import { discoverLayout, listFiles } from '../utils/fs.js';
import { ID_PREFIXES, SVG_FILE_NAMES, GLOBAL_ONLY_TABLES } from '../schema/registry.js';

const KNOWN_CSV_NAMES = new Set(Object.keys(ID_PREFIXES).map((t) => `${t}.csv`));
const KNOWN_SVG_NAMES = new Set(Object.values(SVG_FILE_NAMES).map((s) => `${s}.svg`));

export function validateStructure(dir: string): string[] {
  const issues: string[] = [];
  const layout = discoverLayout(dir);

  // Must have global/
  if (!existsSync(layout.globalDir)) {
    issues.push('global/  missing global/ directory');
    return issues;
  }

  // global/ must have level.csv and grid.csv
  const globalFiles = listFiles(layout.globalDir);
  if (!globalFiles.includes('level.csv')) {
    issues.push('global/  missing level.csv');
  }
  if (!globalFiles.includes('grid.csv')) {
    issues.push('global/  missing grid.csv');
  }

  // No SVG files in global/
  for (const f of globalFiles) {
    if (extname(f) === '.svg') {
      issues.push(`global/${f}  SVG files are not allowed in global/ (SVG is always per-level)`);
    }
  }

  // Check global/ files are known
  for (const f of globalFiles) {
    if (extname(f) === '.csv' && !KNOWN_CSV_NAMES.has(f)) {
      issues.push(`global/${f}  unknown CSV file`);
    }
  }

  // Level dirs must match lv-{n} pattern (from actual level IDs)
  for (const ld of layout.levelDirs) {
    if (!/^lv-\d+$/.test(ld.name)) {
      issues.push(`${ld.name}/  directory name does not match lv-{n} pattern`);
    }

    const files = listFiles(ld.path);
    for (const f of files) {
      const ext = extname(f);
      if (ext === '.csv') {
        if (!KNOWN_CSV_NAMES.has(f)) {
          issues.push(`${ld.name}/${f}  unknown CSV file`);
        }
        // global-only tables shouldn't appear in level dirs
        const tableName = f.replace('.csv', '');
        if (GLOBAL_ONLY_TABLES.has(tableName)) {
          issues.push(`${ld.name}/${f}  ${tableName} belongs in global/ only`);
        }
      } else if (ext === '.svg') {
        if (!KNOWN_SVG_NAMES.has(f)) {
          issues.push(`${ld.name}/${f}  unknown SVG file`);
        }
        // SVG must have matching CSV
        const svgBase = f.replace('.svg', '');
        const tableName = Object.entries(SVG_FILE_NAMES).find(
          ([, v]) => v === svgBase,
        )?.[0];
        if (tableName && !files.includes(`${tableName}.csv`)) {
          issues.push(`${ld.name}/${f}  orphaned SVG — no matching ${tableName}.csv`);
        }
      } else {
        issues.push(`${ld.name}/${f}  unexpected file`);
      }
    }
  }

  return issues;
}
