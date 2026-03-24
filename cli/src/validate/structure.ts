import { existsSync, readdirSync } from 'node:fs';
import { join, extname } from 'node:path';
import { discoverLayout, listFiles } from '../utils/fs.js';
import { ID_PREFIXES, SVG_FILE_NAMES, GLOBAL_ONLY_TABLES } from '../schema/registry.js';

const KNOWN_CSV_NAMES = new Set(Object.keys(ID_PREFIXES).map((t) => `${t}.csv`));
const KNOWN_SVG_NAMES = new Set(Object.values(SVG_FILE_NAMES).map((s) => `${s}.svg`));
const KNOWN_GLOBAL_EXTRAS = new Set(['_IdMap.csv']);

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

  // global/ — only known CSV files allowed, no SVG, no other files
  for (const f of globalFiles) {
    const ext = extname(f);
    if (ext === '.svg') {
      issues.push(`global/${f}  SVG files are not allowed in global/`);
    } else if (ext === '.csv') {
      if (!KNOWN_CSV_NAMES.has(f) && !KNOWN_GLOBAL_EXTRAS.has(f)) {
        issues.push(`global/${f}  unknown file — only known table CSVs are allowed`);
      }
    } else {
      issues.push(`global/${f}  unexpected file — only CSV files are allowed in global/`);
    }
  }

  // Level dirs
  for (const ld of layout.levelDirs) {
    if (!/^lv-\d+$/.test(ld.name)) {
      issues.push(`${ld.name}/  directory name does not match lv-{n} pattern`);
      continue;
    }

    const files = listFiles(ld.path);

    // Reject any file that is not a known CSV or known SVG
    for (const f of files) {
      const ext = extname(f);
      if (ext === '.csv') {
        if (!KNOWN_CSV_NAMES.has(f)) {
          issues.push(`${ld.name}/${f}  unknown file — not a recognized BimDown table`);
        }
        const tableName = f.replace('.csv', '');
        if (GLOBAL_ONLY_TABLES.has(tableName)) {
          issues.push(`${ld.name}/${f}  ${tableName} belongs in global/ only`);
        }
      } else if (ext === '.svg') {
        if (!KNOWN_SVG_NAMES.has(f)) {
          // Give a helpful hint for the common singular mistake
          const base = f.replace('.svg', '');
          const correctEntry = Object.entries(SVG_FILE_NAMES).find(([k]) => k === base);
          if (correctEntry) {
            issues.push(`${ld.name}/${f}  wrong SVG file name — should be "${correctEntry[1]}.svg" (plural), not "${f}"`);
          } else {
            issues.push(`${ld.name}/${f}  unknown file — not a recognized BimDown SVG name`);
          }
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
        issues.push(`${ld.name}/${f}  unexpected file — only CSV and SVG files are allowed`);
      }
    }

    // CSV must have matching SVG (reverse check)
    for (const f of files) {
      if (extname(f) !== '.csv') continue;
      const tableName = f.replace('.csv', '');
      const svgName = SVG_FILE_NAMES[tableName];
      if (svgName && !files.includes(`${svgName}.svg`)) {
        issues.push(`${ld.name}/${f}  missing paired SVG file "${svgName}.svg"`);
      }
    }
  }

  // Reject any top-level entries that are not global/ or lv-{n}/
  const topEntries = readdirSync(dir);
  for (const f of topEntries) {
    if (f === 'global' || /^lv-\d+$/.test(f)) continue;
    if (f.startsWith('.')) continue; // allow hidden dirs like .pi
    issues.push(`${f}  unexpected entry in project root — only global/ and lv-{n}/ directories are allowed`);
  }

  return issues;
}
