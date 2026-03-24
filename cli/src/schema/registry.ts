import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ResolvedTable, ResolvedField } from './types.js';
import { loadAllSchemas, resolveFields } from './loader.js';

export const ID_PREFIXES: Record<string, string> = {
  level: 'lv', grid: 'gr', wall: 'w', column: 'c', slab: 'sl',
  space: 'sp', door: 'd', window: 'wn', stair: 'st',
  structure_wall: 'sw', structure_column: 'sc', structure_slab: 'ss',
  beam: 'bm', brace: 'br', isolated_foundation: 'if',
  strip_foundation: 'sf', raft_foundation: 'rf',
  duct: 'du', pipe: 'pi', cable_tray: 'ct', conduit: 'co',
  equipment: 'eq', terminal: 'tm',
};

// Tables whose CSV lives only in global/ (not per-level)
export const GLOBAL_ONLY_TABLES = new Set(['level', 'grid']);

// Tables that can appear in global/ (cross-floor elements)
export const GLOBAL_ALLOWED_TABLES = new Set([
  'level', 'grid', 'stair',
  'duct', 'pipe', 'cable_tray', 'conduit',
  'equipment', 'terminal',
  'structure_column', 'beam', 'brace',
]);

// SVG file name mapping: table name -> svg file name (without extension)
// SVG files use the same name as the CSV (both singular): wall.csv + wall.svg
export const SVG_FILE_NAMES: Record<string, string> = Object.fromEntries(
  Object.keys(ID_PREFIXES)
    .filter((k) => k !== 'level' && k !== 'grid')
    .map((k) => [k, k]),
);

// Tables that have SVG geometry (not level/grid)
export const TABLES_WITH_SVG = new Set(Object.keys(SVG_FILE_NAMES));

let _registry: Map<string, ResolvedTable> | null = null;
let _specDir: string | null = null;

export function buildRegistry(specDir: string): Map<string, ResolvedTable> {
  if (_registry && _specDir === specDir) return _registry;

  const schemas = loadAllSchemas(join(specDir, 'csv-schema'));
  const resolved = new Map<string, ResolvedField[]>();
  const registry = new Map<string, ResolvedTable>();

  for (const [name, schema] of schemas) {
    if (schema.abstract) continue;

    const prefix = ID_PREFIXES[name];
    if (!prefix) continue; // skip unknown concrete schemas

    const allFields = resolveFields(name, schemas, resolved);
    const csvFields = allFields.filter((f) => !f.computed);
    const computedFields = allFields.filter((f) => f.computed);

    registry.set(name, {
      name,
      prefix,
      hostType: schema.host_type,
      allFields,
      csvFields,
      computedFields,
    });
  }

  _registry = registry;
  _specDir = specDir;
  return registry;
}

export function getSpecDir(): string {
  if (process.env.SPEC_DIR) return process.env.SPEC_DIR;
  // Resolve spec dir relative to this package (cli/ is sibling to spec/)
  const thisFile = fileURLToPath(import.meta.url);
  const thisDir = dirname(thisFile);
  // thisDir is cli/src/schema or cli/dist — go up to cli/, then sibling spec/
  return join(thisDir, '..', '..', 'spec');
}
