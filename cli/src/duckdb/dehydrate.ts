import { existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import type { DuckDBConnection } from '@duckdb/node-api';
import { buildRegistry, getSpecDir } from '../schema/registry.js';
import { writeCsv, type CsvData } from '../utils/csv.js';
import { runQuery } from './engine.js';

export async function dehydrate(conn: DuckDBConnection, dir: string): Promise<void> {
  const registry = buildRegistry(getSpecDir());

  for (const [tableName, table] of registry) {
    // Check if table exists
    const check = await runQuery(
      conn,
      `SELECT count(*) as cnt FROM information_schema.tables WHERE table_name = '${tableName}'`,
    );
    if (!check.rows.length || check.rows[0].cnt === 0n) continue;

    const csvHeaders = table.csvFields.map((f) => f.name);

    // Get distinct partitions
    const partResult = await runQuery(
      conn,
      `SELECT DISTINCT _partition FROM "${tableName}"`,
    );

    for (const partRow of partResult.rows) {
      const partition = String(partRow._partition);
      const outDir = join(dir, partition);
      if (!existsSync(outDir)) mkdirSync(outDir, { recursive: true });

      const selectCols = csvHeaders.map((h) => `"${h}"`).join(', ');
      const dataResult = await runQuery(
        conn,
        `SELECT ${selectCols} FROM "${tableName}" WHERE _partition = '${partition}'`,
      );

      const data: CsvData = {
        headers: csvHeaders,
        rows: dataResult.rows.map((r) => {
          const row: Record<string, string> = {};
          for (const h of csvHeaders) {
            const val = r[h];
            row[h] = val == null ? '' : String(val);
          }
          return row;
        }),
      };

      writeCsv(join(outDir, `${tableName}.csv`), data);
    }
  }
}
