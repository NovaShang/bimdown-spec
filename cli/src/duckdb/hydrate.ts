import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { DuckDBConnection } from '@duckdb/node-api';
import { buildRegistry, getSpecDir, SVG_FILE_NAMES } from '../schema/registry.js';
import type { ResolvedTable } from '../schema/types.js';
import { discoverLayout } from '../utils/fs.js';
import { readCsv } from '../utils/csv.js';
import {
  parseSvgFile,
  extractLineGeometry,
  extractRectGeometry,
  extractPolygonGeometry,
  extractCircleGeometry,
} from '../utils/svg.js';
import { runQuery } from './engine.js';

const INSERT_BATCH_SIZE = 500;

export async function hydrate(conn: DuckDBConnection, dir: string): Promise<string[]> {
  const registry = buildRegistry(getSpecDir());
  const layout = discoverLayout(dir);
  const tables: string[] = [];

  const allDirs = [
    { name: 'global', path: layout.globalDir },
    ...layout.levelDirs,
  ];

  // Pass 1 — create each table and bulk-insert its CSV rows.
  const hydrated: { name: string; table: ResolvedTable }[] = [];
  for (const [tableName, table] of registry) {
    const csvFiles: { dir: string; fullPath: string }[] = [];
    for (const d of allDirs) {
      const csvPath = join(d.path, `${tableName}.csv`);
      if (existsSync(csvPath)) {
        csvFiles.push({ dir: d.name, fullPath: csvPath });
      }
    }
    if (csvFiles.length === 0) continue;

    const allRows: Record<string, string>[] = [];
    let headers: string[] = [];
    for (const { dir: dirName, fullPath } of csvFiles) {
      const data = readCsv(fullPath);
      if (headers.length === 0) headers = data.headers;
      for (const row of data.rows) {
        allRows.push({ ...row, _partition: dirName });
      }
    }
    if (allRows.length === 0) continue;

    const cols = [...headers, '_partition'];
    const colDefs = cols.map((c) => `"${c}" VARCHAR`).join(', ');
    await runQuery(conn, `CREATE TABLE "${tableName}" (${colDefs})`);

    // Batched multi-row INSERTs instead of one query per row.
    for (let i = 0; i < allRows.length; i += INSERT_BATCH_SIZE) {
      const batch = allRows.slice(i, i + INSERT_BATCH_SIZE);
      const valuesSql = batch
        .map((row) => `(${cols.map((c) => escapeSql(row[c] ?? '')).join(', ')})`)
        .join(', ');
      await runQuery(conn, `INSERT INTO "${tableName}" VALUES ${valuesSql}`);
    }

    tables.push(tableName);
    hydrated.push({ name: tableName, table });
  }

  // Pass 2 — inject computed fields. By this point the `level` table (if any)
  // is guaranteed to exist, so `_levels` can be built up front and reused.
  const levelsReady = await tryCreateLevelsTemp(conn);

  for (const { name, table } of hydrated) {
    await injectGeometry(conn, name, allDirs);

    if (levelsReady && table.computedFields.some((f) => f.name === 'height')) {
      await injectHeight(conn, name);
    }
  }

  return tables;
}

/**
 * Build a temporary `_levels(id, elevation, rank)` table from the already-
 * hydrated `level` table. Returns false if the project has no level.csv.
 */
async function tryCreateLevelsTemp(conn: DuckDBConnection): Promise<boolean> {
  const check = await runQuery(
    conn,
    `SELECT table_name FROM information_schema.tables WHERE table_name = 'level'`,
  );
  if (check.rows.length === 0) return false;

  await runQuery(
    conn,
    `CREATE TEMP TABLE _levels AS
     SELECT
       id,
       CAST(elevation AS DOUBLE) AS elevation,
       ROW_NUMBER() OVER (ORDER BY CAST(elevation AS DOUBLE)) AS rank
     FROM level
     WHERE elevation IS NOT NULL AND elevation <> ''`,
  );
  return true;
}

/**
 * Compute `height` for every row of a vertical_span table in a single SQL
 * UPDATE that joins `_levels` twice (base + top). Cascading defaults:
 *   base_level_id: explicit → _partition (non-global) → lowest level
 *   top_level_id:  explicit → next level above base_level_id
 * Rows that can't resolve either side stay NULL (same as the JS fallback).
 */
async function injectHeight(conn: DuckDBConnection, tableName: string): Promise<void> {
  const colsResult = await runQuery(
    conn,
    `SELECT column_name FROM information_schema.columns WHERE table_name = '${tableName}'`,
  );
  const existing = new Set(colsResult.rows.map((r) => String(r.column_name)));

  // Ensure the columns the UPDATE references all exist, so the SQL stays uniform
  // across tables whose CSVs omit some of these fields.
  const ensureVarchar = async (col: string) => {
    if (!existing.has(col)) {
      await runQuery(conn, `ALTER TABLE "${tableName}" ADD COLUMN "${col}" VARCHAR`);
    }
  };
  await ensureVarchar('base_level_id');
  await ensureVarchar('top_level_id');
  await ensureVarchar('base_offset');
  await ensureVarchar('top_offset');
  if (!existing.has('height')) {
    await runQuery(conn, `ALTER TABLE "${tableName}" ADD COLUMN "height" DOUBLE`);
  }

  await runQuery(
    conn,
    `UPDATE "${tableName}" AS t
     SET height = (lv_top.elevation + COALESCE(CAST(NULLIF(t.top_offset, '') AS DOUBLE), 0))
                - (lv_base.elevation + COALESCE(CAST(NULLIF(t.base_offset, '') AS DOUBLE), 0))
     FROM _levels AS lv_base, _levels AS lv_top
     WHERE lv_base.id = COALESCE(
             NULLIF(t.base_level_id, ''),
             CASE WHEN t._partition <> 'global'
                  THEN t._partition
                  ELSE (SELECT id FROM _levels ORDER BY rank LIMIT 1)
             END
           )
       AND lv_top.id = COALESCE(
             NULLIF(t.top_level_id, ''),
             (SELECT id FROM _levels WHERE rank = lv_base.rank + 1)
           )`,
  );
}

/**
 * Inject geometry fields (start_x, end_x, points, ...) from SVG files into the
 * main table. Uses a per-table temp staging table + a single JOIN UPDATE instead
 * of one UPDATE per row.
 */
async function injectGeometry(
  conn: DuckDBConnection,
  tableName: string,
  dirs: { name: string; path: string }[],
): Promise<void> {
  const svgName = SVG_FILE_NAMES[tableName];
  if (!svgName) return;

  const geometries = new Map<string, Record<string, number | string>>();

  for (const d of dirs) {
    // global/ can have SVGs too (multi-story walls, curtain walls, beams).
    const svgPath = join(d.path, `${svgName}.svg`);
    if (!existsSync(svgPath)) continue;

    try {
      const svg = parseSvgFile(svgPath);
      for (const el of svg.elements) {
        if (el.tag === 'path') {
          geometries.set(el.id, extractLineGeometry(el));
        } else if (el.tag === 'rect') {
          geometries.set(el.id, extractRectGeometry(el));
        } else if (el.tag === 'polygon') {
          geometries.set(el.id, extractPolygonGeometry(el));
        } else if (el.tag === 'circle') {
          geometries.set(el.id, extractCircleGeometry(el));
        }
      }
    } catch {
      // Skip unparseable SVGs
    }
  }

  if (geometries.size === 0) return;

  // Union of all geometry field names across collected rows.
  const allKeys = new Set<string>();
  for (const geo of geometries.values()) {
    for (const k of Object.keys(geo)) allKeys.add(k);
  }
  const geoCols = Array.from(allKeys);
  const typeOf = (col: string) => (col === 'points' ? 'VARCHAR' : 'DOUBLE');

  // Add any missing geometry columns on the main table.
  const existingCols = await runQuery(
    conn,
    `SELECT column_name FROM information_schema.columns WHERE table_name = '${tableName}'`,
  );
  const existingSet = new Set(existingCols.rows.map((r) => String(r.column_name)));
  for (const col of geoCols) {
    if (existingSet.has(col)) continue;
    await runQuery(conn, `ALTER TABLE "${tableName}" ADD COLUMN "${col}" ${typeOf(col)}`);
  }

  // Stage into a temp table, then UPDATE ... FROM for a single round-trip.
  const stagingName = `_geo_${tableName}`;
  const stagingColDefs = ['"id" VARCHAR', ...geoCols.map((c) => `"${c}" ${typeOf(c)}`)].join(', ');
  await runQuery(conn, `CREATE TEMP TABLE "${stagingName}" (${stagingColDefs})`);

  const entries = Array.from(geometries.entries());
  for (let i = 0; i < entries.length; i += INSERT_BATCH_SIZE) {
    const batch = entries.slice(i, i + INSERT_BATCH_SIZE);
    const valuesSql = batch
      .map(([id, geo]) => {
        const vals = [escapeSql(id)];
        for (const col of geoCols) {
          const v = geo[col];
          if (v === undefined || v === null) {
            vals.push('NULL');
          } else if (typeof v === 'string') {
            vals.push(escapeSql(v));
          } else {
            vals.push(String(v));
          }
        }
        return `(${vals.join(', ')})`;
      })
      .join(', ');
    await runQuery(conn, `INSERT INTO "${stagingName}" VALUES ${valuesSql}`);
  }

  const setClause = geoCols.map((c) => `"${c}" = g."${c}"`).join(', ');
  await runQuery(
    conn,
    `UPDATE "${tableName}" SET ${setClause} FROM "${stagingName}" g WHERE "${tableName}".id = g.id`,
  );

  await runQuery(conn, `DROP TABLE "${stagingName}"`);
}

function escapeSql(val: string): string {
  return "'" + val.replace(/'/g, "''") + "'";
}
