import type { ResolvedTable } from '../schema/types.js';
import type { CsvData } from '../utils/csv.js';

export function validateIdFormat(
  path: string,
  table: ResolvedTable,
  data: CsvData,
): string[] {
  const issues: string[] = [];
  const prefix = table.prefix;

  for (let i = 0; i < data.rows.length; i++) {
    const id = data.rows[i].id;
    if (!id) continue; // caught by required check

    const regex = new RegExp(`^${prefix}-\\d+$`);
    if (!regex.test(id)) {
      issues.push(`${path}:${i + 2}  id "${id}" has wrong format, expected "${prefix}-{n}"`);
    }
  }

  return issues;
}

export interface IdRegistry {
  // id -> { table, path, row }
  ids: Map<string, { table: string; path: string; row: number }>;
  issues: string[];
}

export function createIdRegistry(): IdRegistry {
  return { ids: new Map(), issues: [] };
}

export function registerIds(
  registry: IdRegistry,
  path: string,
  tableName: string,
  data: CsvData,
): void {
  for (let i = 0; i < data.rows.length; i++) {
    const id = data.rows[i].id;
    if (!id) continue;

    const existing = registry.ids.get(id);
    if (existing) {
      if (existing.table === tableName) {
        registry.issues.push(
          `${path}:${i + 2}  duplicate id "${id}" (first seen in ${existing.path}:${existing.row})`,
        );
      } else {
        registry.issues.push(
          `${path}:${i + 2}  id "${id}" conflicts with ${existing.table} in ${existing.path}:${existing.row}`,
        );
      }
    } else {
      registry.ids.set(id, { table: tableName, path, row: i + 2 });
    }
  }
}
