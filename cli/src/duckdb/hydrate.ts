import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { DuckDBConnection } from '@duckdb/node-api';
import { buildRegistry, getSpecDir, SVG_FILE_NAMES } from '../schema/registry.js';
import { discoverLayout, listFiles } from '../utils/fs.js';
import { readCsv } from '../utils/csv.js';
import {
  parseSvgFile,
  extractLineGeometry,
  extractRectGeometry,
  extractPolygonGeometry,
  extractCircleGeometry,
} from '../utils/svg.js';
import { runQuery } from './engine.js';

export async function hydrate(conn: DuckDBConnection, dir: string): Promise<string[]> {
  const registry = buildRegistry(getSpecDir());
  const layout = discoverLayout(dir);
  const tables: string[] = [];

  const allDirs = [
    { name: 'global', path: layout.globalDir },
    ...layout.levelDirs,
  ];

  for (const [tableName, table] of registry) {
    // Collect all CSV files for this table
    const csvFiles: { dir: string; fullPath: string }[] = [];
    for (const d of allDirs) {
      const csvPath = join(d.path, `${tableName}.csv`);
      if (existsSync(csvPath)) {
        csvFiles.push({ dir: d.name, fullPath: csvPath });
      }
    }

    if (csvFiles.length === 0) continue;

    // Read and union all CSVs
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

    // Create table with CSV columns + _partition
    const cols = [...headers, '_partition'];
    const colDefs = cols.map((c) => `"${c}" VARCHAR`).join(', ');
    await runQuery(conn, `CREATE TABLE "${tableName}" (${colDefs})`);

    // Insert rows in batches
    for (const row of allRows) {
      const values = cols.map((c) => escapeSql(row[c] ?? ''));
      await runQuery(conn, `INSERT INTO "${tableName}" VALUES (${values.join(', ')})`);
    }

    tables.push(tableName);

    // Inject computed geometry from SVG
    await injectGeometry(conn, tableName, allDirs);
  }

  return tables;
}

async function injectGeometry(
  conn: DuckDBConnection,
  tableName: string,
  dirs: { name: string; path: string }[],
): Promise<void> {
  const svgName = SVG_FILE_NAMES[tableName];
  if (!svgName) return;

  // Collect geometry from SVGs across all level dirs
  const geometries = new Map<string, Record<string, number | string>>();

  for (const d of dirs) {
    if (d.name === 'global') continue; // no SVGs in global
    const svgPath = join(d.path, `${svgName}.svg`);
    if (!existsSync(svgPath)) continue;

    try {
      const svg = parseSvgFile(svgPath);
      for (const el of svg.elements) {
        if (el.tag === 'line') {
          const geo = extractLineGeometry(el);
          geometries.set(el.id, geo);
        } else if (el.tag === 'rect') {
          const geo = extractRectGeometry(el);
          geometries.set(el.id, geo);
        } else if (el.tag === 'polygon') {
          const geo = extractPolygonGeometry(el);
          geometries.set(el.id, geo);
        } else if (el.tag === 'circle') {
          const geo = extractCircleGeometry(el);
          geometries.set(el.id, geo);
        }
      }
    } catch {
      // Skip unparseable SVGs
    }
  }

  if (geometries.size === 0) return;

  // Determine geometry columns to add
  const allKeys = new Set<string>();
  for (const geo of geometries.values()) {
    for (const k of Object.keys(geo)) allKeys.add(k);
  }

  // Get existing columns to avoid duplicates
  const existingCols = await runQuery(
    conn,
    `SELECT column_name FROM information_schema.columns WHERE table_name = '${tableName}'`,
  );
  const existingSet = new Set(existingCols.rows.map((r) => String(r.column_name)));

  for (const col of allKeys) {
    if (existingSet.has(col)) continue;
    const sqlType = col === 'points' ? 'VARCHAR' : 'DOUBLE';
    await runQuery(conn, `ALTER TABLE "${tableName}" ADD COLUMN "${col}" ${sqlType}`);
  }

  // Update each element with its geometry
  for (const [id, geo] of geometries) {
    const sets = Object.entries(geo)
      .map(([k, v]) => `"${k}" = ${typeof v === 'string' ? escapeSql(v) : v}`)
      .join(', ');
    await runQuery(conn, `UPDATE "${tableName}" SET ${sets} WHERE id = ${escapeSql(id)}`);
  }
}

function escapeSql(val: string): string {
  return "'" + val.replace(/'/g, "''") + "'";
}
