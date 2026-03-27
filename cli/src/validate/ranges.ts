/**
 * Value range validation — catches millimeter-vs-meter mistakes.
 *
 * Any float field value > threshold is flagged as a likely unit error.
 * Field-specific thresholds override the default where needed.
 */
import type { ResolvedTable } from '../schema/types.js';
import type { CsvData } from '../utils/csv.js';

/** Per-field max reasonable values in meters. */
const FIELD_MAX: Record<string, number> = {
  elevation: 500,       // tallest buildings ~500m
  base_offset: 100,
  top_offset: 100,
  thickness: 2,
  width: 30,
  height: 20,
  size_x: 10,
  size_y: 10,
  length: 100,
  position: 1,          // parametric 0-1
  start_x: 10000,      // site coordinates can be large
  start_y: 10000,
  end_x: 10000,
  end_y: 10000,
  start_z: 500,
  end_z: 500,
  step_count: 200,      // not meters, but still bounded
};

/** Default max for any float field not in FIELD_MAX. */
const DEFAULT_MAX = 1000;

export function validateRanges(path: string, table: ResolvedTable, data: CsvData): string[] {
  const issues: string[] = [];

  const floatFields = table.csvFields.filter(
    (f) => (f.type === 'float' || f.type === 'int') && f.name !== 'id',
  );

  for (let i = 0; i < data.rows.length; i++) {
    const row = data.rows[i];
    const rowNum = i + 2; // 1-indexed + header

    for (const field of floatFields) {
      const raw = row[field.name];
      if (raw === undefined || raw === '') continue;

      const val = Number(raw);
      if (isNaN(val)) continue;

      const max = FIELD_MAX[field.name] ?? DEFAULT_MAX;
      if (Math.abs(val) > max) {
        issues.push(
          `${path}:${rowNum}  ${field.name}=${raw} looks like millimeters — all values must be in meters (max reasonable: ${max})`,
        );
      }
    }
  }

  return issues;
}
