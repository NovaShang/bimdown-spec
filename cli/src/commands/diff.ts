import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, discoverLayout } from '../utils/fs.js';
import { readCsv } from '../utils/csv.js';

export function diffProjects(dirA: string, dirB: string) {
  if (!existsSync(dirA)) {
    console.error(`Directory not found: ${dirA}`);
    process.exitCode = 1;
    return;
  }
  if (!existsSync(dirB)) {
    console.error(`Directory not found: ${dirB}`);
    process.exitCode = 1;
    return;
  }

  const layoutA = discoverLayout(dirA);
  const layoutB = discoverLayout(dirB);

  // Collect all distinct paths (e.g., global, lv-1, lv-2) relative to root
  const pathsA = [
    { name: 'global', abs: layoutA.globalDir },
    ...layoutA.levelDirs.map(d => ({ name: d.name, abs: d.path }))
  ];
  const pathsB = [
    { name: 'global', abs: layoutB.globalDir },
    ...layoutB.levelDirs.map(d => ({ name: d.name, abs: d.path }))
  ];

  const allPathNames = new Set([...pathsA.map(p => p.name), ...pathsB.map(p => p.name)]);

  for (const pathName of allPathNames) {
    const dirAAbs = pathsA.find(p => p.name === pathName)?.abs;
    const dirBAbs = pathsB.find(p => p.name === pathName)?.abs;

    const filesA = new Set(dirAAbs && existsSync(dirAAbs) ? listFiles(dirAAbs).filter(f => f.endsWith('.csv')) : []);
    const filesB = new Set(dirBAbs && existsSync(dirBAbs) ? listFiles(dirBAbs).filter(f => f.endsWith('.csv')) : []);

    const allFiles = new Set([...filesA, ...filesB]);

    let printedPathHeader = false;

    for (const f of allFiles) {
      const tableName = f.replace('.csv', '');
      const pathFileA = dirAAbs ? join(dirAAbs, f) : null;
      const pathFileB = dirBAbs ? join(dirBAbs, f) : null;

      const rowsA: Record<string, any> = {};
      const rowsB: Record<string, any> = {};

      if (pathFileA && existsSync(pathFileA)) {
        for (const row of readCsv(pathFileA).rows) {
          if (row.id) rowsA[String(row.id)] = row;
        }
      }

      if (pathFileB && existsSync(pathFileB)) {
        for (const row of readCsv(pathFileB).rows) {
          if (row.id) rowsB[String(row.id)] = row;
        }
      }

      const allIds = new Set([...Object.keys(rowsA), ...Object.keys(rowsB)]);
      const diffs: { sign: string; id: string }[] = [];

      for (const id of allIds) {
        if (!rowsA[id] && rowsB[id]) {
          diffs.push({ sign: '+', id });
        } else if (rowsA[id] && !rowsB[id]) {
          diffs.push({ sign: '-', id });
        } else {
          // Both exist, check equality
          if (JSON.stringify(rowsA[id]) !== JSON.stringify(rowsB[id])) {
            diffs.push({ sign: '~', id });
          }
        }
      }

      if (diffs.length > 0) {
        if (!printedPathHeader) {
          console.log(`\n📁 ${pathName}`);
          printedPathHeader = true;
        }
        console.log(`  == ${tableName} ==`);
        for (const d of diffs) {
          console.log(`  ${d.sign} ${d.id}`);
        }
      }
    }
  }

  console.log();
}
