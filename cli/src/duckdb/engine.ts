import { DuckDBInstance, DuckDBConnection } from '@duckdb/node-api';

let instance: DuckDBInstance | null = null;
let connection: DuckDBConnection | null = null;

export async function getConnection(): Promise<DuckDBConnection> {
  if (connection) return connection;
  instance = await DuckDBInstance.create(':memory:');
  connection = await instance.connect();
  return connection;
}

export async function closeConnection(): Promise<void> {
  connection = null;
  instance = null;
}

export interface QueryResult {
  columns: string[];
  rows: Record<string, unknown>[];
}

export async function runQuery(conn: DuckDBConnection, sql: string): Promise<QueryResult> {
  const reader = await conn.runAndReadAll(sql);
  const columns = reader.columnNames();
  const rows = reader.getRowObjects();
  return { columns, rows };
}
