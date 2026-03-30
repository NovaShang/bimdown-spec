import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ResolvedTable, ResolvedField } from './types.js';
import { loadAllSchemas, resolveFields } from './loader.js';

export const ID_PREFIXES: Record<string, string> = {
  level: 'lv', grid: 'gr', wall: 'w', column: 'c', slab: 'sl',
  space: 'sp', door: 'd', window: 'wn', stair: 'st',
  ramp: 'rp', railing: 'rl', room_separator: 'rs',
  curtain_wall: 'cw', roof: 'ro', ceiling: 'cl', opening: 'op',
  structure_wall: 'sw', structure_column: 'sc', structure_slab: 'ss',
  beam: 'bm', brace: 'br', foundation: 'f',
  duct: 'du', pipe: 'pi', cable_tray: 'ct', conduit: 'co',
  equipment: 'eq', terminal: 'tm', mep_node: 'mn',
  mesh: 'mesh',
};

// Tables whose CSV lives only in global/ (not per-level)
export const GLOBAL_ONLY_TABLES = new Set(['level', 'grid', 'mesh']);

// Tables that can appear in global/ (cross-floor elements)
export const GLOBAL_ALLOWED_TABLES = new Set([
  'level', 'grid', 'stair',
  'duct', 'pipe', 'cable_tray', 'conduit',
  'equipment', 'terminal',
  'structure_column', 'beam', 'brace',
]);

// Tables without SVG geometry (level/grid are global-only, door/window use CSV position)
const TABLES_WITHOUT_SVG = new Set(['level', 'grid', 'door', 'window', 'space', 'mesh']);

// SVG file name mapping: table name -> svg file name (without extension)
// SVG files use the same name as the CSV (both singular): wall.csv + wall.svg
export const SVG_FILE_NAMES: Record<string, string> = Object.fromEntries(
  Object.keys(ID_PREFIXES)
    .filter((k) => !TABLES_WITHOUT_SVG.has(k))
    .map((k) => [k, k]),
);

// Tables that have SVG geometry
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
