import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, extname } from 'node:path';
import { discoverLayout, listFiles } from '../utils/fs.js';
import { ID_PREFIXES, SVG_FILE_NAMES, GLOBAL_ONLY_TABLES } from '../schema/registry.js';

// Tables where SVG is optional (opening: wall-mode has no SVG, slab-mode does)
const OPTIONAL_SVG_TABLES = new Set(['opening', 'space']);

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
    if (!/^lv-.+$/.test(ld.name)) {
      issues.push(`${ld.name}/  directory name does not match lv-{id} pattern`);
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
          issues.push(`${ld.name}/${f}  unknown SVG file — not a recognized BimDown table name`);
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

    // CSV must have matching SVG (reverse check) — unless SVG is optional for that table
    for (const f of files) {
      if (extname(f) !== '.csv') continue;
      const tableName = f.replace('.csv', '');
      const svgName = SVG_FILE_NAMES[tableName];
      if (svgName && !files.includes(`${svgName}.svg`) && !OPTIONAL_SVG_TABLES.has(tableName)) {
        issues.push(`${ld.name}/${f}  missing paired SVG file "${svgName}.svg"`);
      }
    }
  }

  // Reject any top-level entries that are not global/ or lv-{n}/ or project_metadata.json
  const topEntries = readdirSync(dir);
  for (const f of topEntries) {
    if (f === 'global' || /^lv-.+$/.test(f)) continue;
    if (f === 'project_metadata.json') continue;
    if (f.startsWith('.')) continue; // allow hidden dirs like .pi
    issues.push(`${f}  unexpected entry in project root — only global/ and lv-{n}/ directories are allowed`);
  }

  // Validate project_metadata.json if present
  const metadataPath = join(dir, 'project_metadata.json');
  if (existsSync(metadataPath)) {
    try {
      const raw = readFileSync(metadataPath, 'utf-8');
      const metadata = JSON.parse(raw);
      if (typeof metadata !== 'object' || metadata === null || Array.isArray(metadata)) {
        issues.push('project_metadata.json  must be a JSON object');
      } else {
        if (!metadata.format_version || typeof metadata.format_version !== 'string') {
          issues.push('project_metadata.json  missing or invalid "format_version" (required string)');
        }
        for (const key of Object.keys(metadata)) {
          if (!['format_version', 'project_name', 'units', 'source'].includes(key)) {
            issues.push(`project_metadata.json  unknown field "${key}"`);
          }
        }
      }
    } catch (e) {
      issues.push(`project_metadata.json  failed to parse: ${(e as Error).message}`);
    }
  }

  return issues;
}
