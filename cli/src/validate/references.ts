import type { ResolvedTable } from '../schema/types.js';
import type { CsvData } from '../utils/csv.js';
import type { IdRegistry } from './ids.js';
import { ID_PREFIXES } from '../schema/registry.js';

export function validateReferences(
  path: string,
  table: ResolvedTable,
  data: CsvData,
  idRegistry: IdRegistry,
): string[] {
  const issues: string[] = [];
  const refFields = table.csvFields.filter((f) => f.type === 'reference');

  for (let i = 0; i < data.rows.length; i++) {
    const row = data.rows[i];
    for (const f of refFields) {
      const val = row[f.name];
      if (!val) continue; // empty is OK if not required (required check handles that)

      const entry = idRegistry.ids.get(val);
      if (!entry) {
        issues.push(`${path}:${i + 2}  ${f.name} "${val}" references non-existent id`);
        continue;
      }

      // If reference specifies a target table, validate prefix
      if (f.reference && f.reference !== 'element') {
        const expectedPrefix = ID_PREFIXES[f.reference];
        if (expectedPrefix && !val.startsWith(expectedPrefix + '-')) {
          issues.push(
            `${path}:${i + 2}  ${f.name} "${val}" should reference ${f.reference} (prefix "${expectedPrefix}-")`,
          );
        }
      }
    }
  }

  return issues;
}
