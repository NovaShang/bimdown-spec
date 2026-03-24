import { Command } from 'commander';
import { resolve } from 'node:path';
import { validate } from './validate/index.js';
import { buildRegistry, getSpecDir } from './schema/registry.js';
import { discoverLayout, listFiles } from './utils/fs.js';
import { readCsv } from './utils/csv.js';
import { existsSync } from 'node:fs';
import { join } from 'node:path';

const program = new Command();

program
  .name('bimdown')
  .description('BimDown CLI — validate, query, and manage BimDown projects')
  .version('0.1.0');

// ─── validate ───────────────────────────────────────────
program
  .command('validate')
  .argument('<dir>', 'BimDown project directory')
  .description('Validate a BimDown project directory')
  .action((dir: string) => {
    const absDir = resolve(dir);
    const issues = validate(absDir);
    if (issues.length === 0) {
      console.log('No issues found.');
    } else {
      for (const issue of issues) {
        console.log(issue);
      }
      console.log(`\n${issues.length} issue(s) found.`);
      process.exitCode = 1;
    }
  });

// ─── query ──────────────────────────────────────────────
program
  .command('query')
  .argument('<dir>', 'BimDown project directory')
  .argument('<sql>', 'SQL query to execute')
  .option('--json', 'Output results as JSON')
  .description('Hydrate into DuckDB and execute a SQL query')
  .action(async (dir: string, sql: string, opts: { json?: boolean }) => {
    const absDir = resolve(dir);
    const { getConnection, runQuery, closeConnection } = await import('./duckdb/engine.js');
    const { hydrate } = await import('./duckdb/hydrate.js');

    const conn = await getConnection();
    try {
      await hydrate(conn, absDir);
      const result = await runQuery(conn, sql);

      if (opts.json) {
        console.log(JSON.stringify(result.rows, jsonReplacer, 2));
      } else {
        printTable(result.columns, result.rows);
      }
    } finally {
      await closeConnection();
    }
  });

// ─── sync ───────────────────────────────────────────────
program
  .command('sync')
  .argument('<dir>', 'BimDown project directory')
  .description('Hydrate, then dehydrate back (sync DuckDB changes to files)')
  .action(async (dir: string) => {
    const absDir = resolve(dir);
    const { getConnection, closeConnection } = await import('./duckdb/engine.js');
    const { hydrate } = await import('./duckdb/hydrate.js');
    const { dehydrate } = await import('./duckdb/dehydrate.js');

    const conn = await getConnection();
    try {
      const tables = await hydrate(conn, absDir);
      console.log(`Hydrated ${tables.length} tables: ${tables.join(', ')}`);
      await dehydrate(conn, absDir);
      console.log('Sync complete — files updated.');
    } finally {
      await closeConnection();
    }
  });

// ─── info ───────────────────────────────────────────────
program
  .command('info')
  .argument('<dir>', 'BimDown project directory')
  .description('Print project summary')
  .action((dir: string) => {
    const absDir = resolve(dir);
    const registry = buildRegistry(getSpecDir());
    const layout = discoverLayout(absDir);

    // Levels
    const levelCsv = join(layout.globalDir, 'level.csv');
    if (existsSync(levelCsv)) {
      const levels = readCsv(levelCsv);
      console.log('Levels:');
      for (const row of levels.rows) {
        console.log(`  ${row.id}  ${row.name || row.number || ''}  elevation=${row.elevation}`);
      }
      console.log();
    }

    // Element counts per level
    const allDirs = [
      { name: 'global', path: layout.globalDir },
      ...layout.levelDirs,
    ];

    const totals: Record<string, number> = {};

    for (const d of allDirs) {
      if (!existsSync(d.path)) continue;
      const files = listFiles(d.path);
      const counts: string[] = [];
      for (const f of files) {
        if (!f.endsWith('.csv')) continue;
        const tableName = f.replace('.csv', '');
        if (!registry.has(tableName)) continue;
        const data = readCsv(join(d.path, f));
        counts.push(`${tableName}: ${data.rows.length}`);
        totals[tableName] = (totals[tableName] ?? 0) + data.rows.length;
      }
      if (counts.length > 0) {
        console.log(`${d.name}/  ${counts.join(', ')}`);
      }
    }

    console.log();
    console.log('Totals:');
    let grand = 0;
    for (const [table, count] of Object.entries(totals).sort(([a], [b]) => a.localeCompare(b))) {
      console.log(`  ${table}: ${count}`);
      grand += count;
    }
    console.log(`  TOTAL: ${grand}`);
  });

// ─── merge ───────────────────────────────────────────────
program
  .command('merge')
  .argument('<dirs...>', 'Two or more BimDown project directories to merge')
  .requiredOption('-o, --output <dir>', 'Output directory for merged project')
  .description('Merge multiple BimDown project directories into one, resolving ID conflicts')
  .action(async (dirs: string[], opts: { output: string }) => {
    if (dirs.length < 2) {
      console.error('At least 2 source directories are required.');
      process.exitCode = 1;
      return;
    }
    const absDirs = dirs.map((d) => resolve(d));
    const absOut = resolve(opts.output);
    const { merge } = await import('./commands/merge.js');
    await merge(absDirs, absOut);
  });

// ─── schema ─────────────────────────────────────────────
program
  .command('schema')
  .argument('[table]', 'Table name (omit for all tables)')
  .description('Print resolved schema for a table or all tables')
  .action((table?: string) => {
    const registry = buildRegistry(getSpecDir());

    const tables = table ? [table] : [...registry.keys()].sort();
    for (const t of tables) {
      const resolved = registry.get(t);
      if (!resolved) {
        console.error(`Unknown table: ${t}`);
        process.exitCode = 1;
        return;
      }

      console.log(`── ${resolved.name} (prefix: ${resolved.prefix}) ──`);
      if (resolved.hostType) {
        console.log(`  host_type: ${resolved.hostType}`);
      }
      console.log('  CSV fields:');
      for (const f of resolved.csvFields) {
        const tags: string[] = [];
        if (f.required) tags.push('required');
        if (f.type === 'reference') tags.push(`ref:${f.reference}`);
        if (f.type === 'enum') tags.push(`enum:[${f.values?.join('|')}]`);
        const tagStr = tags.length > 0 ? `  (${tags.join(', ')})` : '';
        console.log(`    ${f.name}: ${f.type}${tagStr}`);
      }
      if (resolved.computedFields.length > 0) {
        console.log('  Computed fields (from SVG):');
        for (const f of resolved.computedFields) {
          console.log(`    ${f.name}: ${f.type}`);
        }
      }
      console.log();
    }
  });

program.parse();

// ─── helpers ────────────────────────────────────────────

function printTable(columns: string[], rows: Record<string, unknown>[]) {
  if (rows.length === 0) {
    console.log('(empty result)');
    return;
  }

  // Calculate column widths
  const widths = columns.map((c) => c.length);
  for (const row of rows) {
    for (let i = 0; i < columns.length; i++) {
      const val = formatValue(row[columns[i]]);
      widths[i] = Math.max(widths[i], val.length);
    }
  }

  // Header
  const header = columns.map((c, i) => c.padEnd(widths[i])).join(' | ');
  console.log(header);
  console.log(widths.map((w) => '-'.repeat(w)).join('-+-'));

  // Rows
  for (const row of rows) {
    const line = columns
      .map((c, i) => formatValue(row[c]).padEnd(widths[i]))
      .join(' | ');
    console.log(line);
  }

  console.log(`\n(${rows.length} row(s))`);
}

function formatValue(val: unknown): string {
  if (val == null) return '';
  if (typeof val === 'bigint') return val.toString();
  return String(val);
}

function jsonReplacer(_key: string, value: unknown): unknown {
  if (typeof value === 'bigint') return Number(value);
  return value;
}
