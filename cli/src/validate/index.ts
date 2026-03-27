import { existsSync } from 'node:fs';
import { join, relative } from 'node:path';
import { buildRegistry, getSpecDir, SVG_FILE_NAMES, GLOBAL_ONLY_TABLES } from '../schema/registry.js';
import type { ResolvedTable } from '../schema/types.js';
import { discoverLayout, listFiles } from '../utils/fs.js';
import { readCsv, type CsvData } from '../utils/csv.js';
import { validateStructure } from './structure.js';
import { validateCsvHeaders, validateCsvRequired, validateCsvEnums } from './csv.js';
import { validateIdFormat, createIdRegistry, registerIds, type IdRegistry } from './ids.js';
import { validateReferences } from './references.js';
import { validateRanges } from './ranges.js';
import { validateSvgFile } from './svg.js';

interface CsvEntry {
  path: string;       // relative display path
  fullPath: string;
  tableName: string;
  table: ResolvedTable;
  data: CsvData;
}

export function validate(dir: string): string[] {
  const issues: string[] = [];

  // 1. Structure validation
  issues.push(...validateStructure(dir));

  const registry = buildRegistry(getSpecDir());
  const layout = discoverLayout(dir);

  // Collect all CSVs
  const csvEntries: CsvEntry[] = [];
  const allDirs = [
    { name: 'global', path: layout.globalDir },
    ...layout.levelDirs,
  ];

  for (const d of allDirs) {
    if (!existsSync(d.path)) continue;
    const files = listFiles(d.path);
    for (const f of files) {
      if (!f.endsWith('.csv')) continue;
      const tableName = f.replace('.csv', '');
      const table = registry.get(tableName);
      if (!table) continue;

      const fullPath = join(d.path, f);
      const relPath = `${d.name}/${f}`;

      let data: CsvData;
      try {
        data = readCsv(fullPath);
      } catch (e) {
        issues.push(`${relPath}  failed to read CSV: ${(e as Error).message}`);
        continue;
      }

      csvEntries.push({ path: relPath, fullPath, tableName, table, data });
    }
  }

  // 2-3. CSV header and required field validation
  for (const entry of csvEntries) {
    issues.push(...validateCsvHeaders(entry.path, entry.table, entry.data));
    issues.push(...validateCsvRequired(entry.path, entry.table, entry.data));
  }

  // 4. ID format validation
  for (const entry of csvEntries) {
    issues.push(...validateIdFormat(entry.path, entry.table, entry.data));
  }

  // 5. ID uniqueness (global)
  const idRegistry = createIdRegistry();
  for (const entry of csvEntries) {
    registerIds(idRegistry, entry.path, entry.tableName, entry.data);
  }
  issues.push(...idRegistry.issues);

  // 6. Enum validation
  for (const entry of csvEntries) {
    issues.push(...validateCsvEnums(entry.path, entry.table, entry.data));
  }

  // 7. Reference validation
  for (const entry of csvEntries) {
    issues.push(...validateReferences(entry.path, entry.table, entry.data, idRegistry));
  }

  // 8. Value range validation (catches mm vs m mistakes)
  for (const entry of csvEntries) {
    issues.push(...validateRanges(entry.path, entry.table, entry.data));
  }

  // 8b. Hosted element position validation (0-1 range)
  for (const entry of csvEntries) {
    if (!entry.table.hostType) continue;
    for (let i = 0; i < entry.data.rows.length; i++) {
      const row = entry.data.rows[i];
      const pos = row.position;
      if (pos === undefined || pos === '') continue;
      const val = Number(pos);
      if (isNaN(val) || val < 0 || val > 1) {
        issues.push(
          `${entry.path}:${i + 2}  position=${pos} must be between 0.0 and 1.0`,
        );
      }
    }
  }

  // 9. SVG validation
  for (const d of allDirs) {
    if (!existsSync(d.path) || d.name === 'global') continue;
    const files = listFiles(d.path);
    for (const f of files) {
      if (!f.endsWith('.svg')) continue;
      const svgBase = f.replace('.svg', '');
      const tableEntry = Object.entries(SVG_FILE_NAMES).find(([, v]) => v === svgBase);
      if (!tableEntry) continue;
      const [tableName] = tableEntry;
      const table = registry.get(tableName);
      if (!table) continue;

      // Gather CSV IDs for this table in this level dir
      const csvIds = new Set<string>();
      for (const entry of csvEntries) {
        if (entry.tableName === tableName) {
          const entryDir = entry.path.split('/')[0];
          if (entryDir === d.name) {
            for (const row of entry.data.rows) {
              if (row.id) csvIds.add(row.id);
            }
          }
        }
      }

      const svgFullPath = join(d.path, f);
      const relPath = `${d.name}/${f}`;
      const isHosted = !!table.hostType;
      issues.push(...validateSvgFile(relPath, svgFullPath, csvIds, isHosted, tableName));
    }
  }

  return issues;
}
