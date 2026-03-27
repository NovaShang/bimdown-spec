/**
 * Generate a complete SKILL.md from spec/csv-schema definitions.
 * Output covers all tables, CSV columns, SVG geometry rules, ID prefixes,
 * and CLI validation instructions.
 */
import { buildRegistry, getSpecDir, ID_PREFIXES, SVG_FILE_NAMES, GLOBAL_ONLY_TABLES, TABLES_WITH_SVG } from '../schema/registry.js';
import type { ResolvedTable, ResolvedField } from '../schema/types.js';

const SVG_GEOMETRY: Record<string, string> = {
  wall: '<line> with x1,y1,x2,y2 (wall centerline). stroke-width for rendering only, thickness is in CSV.',
  room_separator: '<line> with x1,y1,x2,y2 (virtual room boundary line, no thickness).',
  column: '<circle> (round) or <rect> (rectangular) at column center.',
  slab: '<polygon> with points attribute outlining the slab boundary.',
  roof: '<polygon> outlining the roof footprint. 3D shape derived from roof_type + slope.',
  ceiling: '<polygon> outlining the ceiling boundary.',
  stair: '<polygon> outlining the stair footprint.',
  curtain_wall: '<line> with x1,y1,x2,y2 (curtain wall centerline).',
  structure_wall: '<line> with x1,y1,x2,y2 (structural wall centerline).',
  structure_column: '<circle> or <rect> at column center.',
  structure_slab: '<polygon> outlining the structural slab.',
  beam: '<line> with x1,y1,x2,y2 (beam centerline).',
  brace: '<line> with x1,y1,x2,y2 (brace centerline).',
  isolated_foundation: '<rect> or <circle> at foundation location.',
  strip_foundation: '<line> with x1,y1,x2,y2 (strip foundation centerline).',
  raft_foundation: '<polygon> outlining the raft boundary.',
  duct: '<line> with x1,y1,x2,y2 (duct centerline).',
  pipe: '<line> with x1,y1,x2,y2 (pipe centerline).',
  cable_tray: '<line> with x1,y1,x2,y2 (cable tray centerline).',
  conduit: '<line> with x1,y1,x2,y2 (conduit centerline).',
  equipment: '<rect> at equipment location.',
  terminal: '<circle> or <rect> at terminal location.',
};

function formatField(f: ResolvedField): string {
  const parts = [f.name];
  const tags: string[] = [];
  if (f.required) tags.push('required');
  if (f.type === 'reference' && f.reference) tags.push(`ref → ${f.reference}`);
  if (f.type === 'enum' && f.values) tags.push(`enum: ${f.values.join(' | ')}`);
  if (tags.length > 0) parts.push(`(${tags.join(', ')})`);
  return parts.join(' ');
}

function generateTableSection(table: ResolvedTable): string {
  const lines: string[] = [];
  const svgFile = SVG_FILE_NAMES[table.name];
  const isGlobal = GLOBAL_ONLY_TABLES.has(table.name);
  const hasSvg = TABLES_WITH_SVG.has(table.name);

  lines.push(`### ${table.name}`);
  lines.push('');

  // Location
  if (isGlobal) {
    lines.push(`- File: \`global/${table.name}.csv\``);
  } else {
    lines.push(`- CSV: \`lv-{n}/${table.name}.csv\``);
    if (hasSvg) lines.push(`- SVG: \`lv-{n}/${svgFile}.svg\``);
  }
  lines.push(`- ID prefix: \`${table.prefix}-\` (e.g. \`${table.prefix}-1\`, \`${table.prefix}-2\`)`);
  if (table.hostType) lines.push(`- Hosted on: ${table.hostType}`);
  lines.push('');

  // CSV columns
  lines.push('**CSV columns:**');
  lines.push('```');
  lines.push(table.csvFields.map(f => f.name).join(','));
  lines.push('```');
  lines.push('');

  // Field details (only non-obvious ones)
  const detailedFields = table.csvFields.filter(f => f.required || f.type === 'enum' || f.type === 'reference');
  if (detailedFields.length > 0) {
    for (const f of detailedFields) {
      lines.push(`- ${formatField(f)}`);
    }
    lines.push('');
  }

  // SVG geometry
  if (hasSvg && SVG_GEOMETRY[table.name]) {
    lines.push(`**SVG geometry:** ${SVG_GEOMETRY[table.name]}`);
    lines.push('');
  }

  return lines.join('\n');
}

export function generateSkill(): string {
  const registry = buildRegistry(getSpecDir());
  const lines: string[] = [];

  lines.push('---');
  lines.push('name: bimdown');
  lines.push('description: Create and modify building models in BimDown format. Most elements use CSV + SVG pairs; doors, windows, and spaces are CSV-only. Covers architecture, structure, and MEP disciplines.');
  lines.push('---');
  lines.push('');
  lines.push('# BimDown Format');
  lines.push('');
  lines.push('BimDown is an AI-native building data format. Most elements use paired CSV (attributes) + SVG (2D geometry) files. Hosted elements (doors, windows) and spaces are CSV-only.');
  lines.push('');

  // Project structure
  lines.push('## Project Structure');
  lines.push('');
  lines.push('```');
  lines.push('project/');
  lines.push('  global/');
  lines.push('    level.csv          # Building levels (required)');
  lines.push('    grid.csv           # Structural grids (optional)');
  lines.push('  lv-1/                # One directory per level');
  lines.push('    wall.csv + wall.svg');
  lines.push('    door.csv              # CSV-only (position on host wall)');
  lines.push('    window.csv            # CSV-only (position on host wall)');
  lines.push('    column.csv + column.svg');
  lines.push('    slab.csv + slab.svg');
  lines.push('    space.csv             # CSV-only (seed point x,y + name)');
  lines.push('    room_separator.csv + room_separator.svg  # virtual boundary lines');
  lines.push('    ...more element types');
  lines.push('  lv-2/');
  lines.push('    ...');
  lines.push('```');
  lines.push('');

  // Key rules
  lines.push('## Key Rules');
  lines.push('');
  lines.push('1. **CSV and SVG are linked by `id`**: Elements with SVG have a unique id in both CSV row and SVG element `id` attribute.');
  lines.push('2. **CSV and SVG use the same file name** (both singular): `wall.csv` pairs with `wall.svg`.');
  lines.push('3. **Coordinates are in meters**, Y-axis points up. SVG uses `<g transform="scale(1,-1)">` to flip Y.');
  lines.push('4. **IDs are unique within each level** and use prefix + number: e.g. `w-1`, `d-2`, `c-3`.');
  lines.push('5. **Doors/windows are CSV-only** — no SVG file. Use `host_id` + `position` (0.0-1.0 along host wall, center of opening).');
  lines.push('6. **Spaces are CSV-only** — seed point (x, y) inside the room, boundary auto-derived from walls + room_separators.');
  lines.push('7. **Wall thickness is a CSV field** — SVG `stroke-width` is for rendering only.');
  lines.push('8. **Defaults**: `base_offset` defaults to 0, `top_level_id` defaults to next level above.');
  lines.push('');

  // SVG template
  lines.push('## SVG Template');
  lines.push('');
  lines.push('All SVG files follow this structure:');
  lines.push('```xml');
  lines.push('<?xml version="1.0" encoding="utf-8"?>');
  lines.push('<svg xmlns="http://www.w3.org/2000/svg" viewBox="x y w h">');
  lines.push('  <g transform="scale(1,-1)">');
  lines.push('    <!-- elements here -->');
  lines.push('  </g>');
  lines.push('</svg>');
  lines.push('```');
  lines.push('Set `viewBox` to a tight bounding box around all elements with ~1m padding.');
  lines.push('');

  // Group tables by discipline
  const architecture = ['wall', 'door', 'window', 'column', 'slab', 'roof', 'ceiling', 'space', 'opening', 'room_separator', 'stair', 'curtain_wall'];
  const structure = ['structure_wall', 'structure_column', 'structure_slab', 'beam', 'brace', 'isolated_foundation', 'strip_foundation', 'raft_foundation'];
  const mep = ['duct', 'pipe', 'cable_tray', 'conduit', 'equipment', 'terminal'];
  const global = ['level', 'grid', 'mesh'];

  lines.push('## Global Tables');
  lines.push('');
  for (const name of global) {
    const table = registry.get(name);
    if (table) lines.push(generateTableSection(table));
  }

  lines.push('## Architecture');
  lines.push('');
  for (const name of architecture) {
    const table = registry.get(name);
    if (table) lines.push(generateTableSection(table));
  }

  lines.push('## Structure');
  lines.push('');
  for (const name of structure) {
    const table = registry.get(name);
    if (table) lines.push(generateTableSection(table));
  }

  lines.push('## MEP');
  lines.push('');
  for (const name of mep) {
    const table = registry.get(name);
    if (table) lines.push(generateTableSection(table));
  }

  // Validation rules
  lines.push('## Validation');
  lines.push('');
  lines.push('After writing files, ALWAYS validate:');
  lines.push('```bash');
  lines.push('bimdown validate /project');
  lines.push('```');
  lines.push('Fix all reported issues before proceeding. Common errors:');
  lines.push('- Missing SVG file for a CSV that requires one (walls, columns, slabs need SVG; doors, windows, spaces do not)');
  lines.push('- Wrong column names (use `bimdown schema <table>` to check)');
  lines.push('- Missing required fields (id is always required)');
  lines.push('- Wrong ID prefix (e.g. `col-1` instead of `c-1`)');
  lines.push('- Door/window position out of 0-1 range');
  lines.push('');

  // CLI tools
  lines.push('## Available CLI Tools');
  lines.push('');
  lines.push('```bash');
  lines.push('bimdown validate /project          # Check all files for errors');
  lines.push('bimdown info /project              # Print project summary (levels, element counts)');
  lines.push('bimdown schema <table>             # Print column definitions for a table');
  lines.push('bimdown query /project "<sql>"     # SQL query on project data (DuckDB)');
  lines.push('```');
  lines.push('');

  // Example
  lines.push('## Example: Simple Room with Door');
  lines.push('');
  lines.push('### global/level.csv');
  lines.push('```csv');
  lines.push('id,number,name,elevation');
  lines.push('lv-1,,Ground Floor,0');
  lines.push('lv-2,,First Floor,3.5');
  lines.push('```');
  lines.push('');
  lines.push('### lv-1/wall.csv');
  lines.push('```csv');
  lines.push('id,material,thickness');
  lines.push('w-1,concrete,0.2');
  lines.push('w-2,concrete,0.2');
  lines.push('w-3,concrete,0.2');
  lines.push('w-4,concrete,0.2');
  lines.push('```');
  lines.push('');
  lines.push('### lv-1/wall.svg');
  lines.push('```xml');
  lines.push('<?xml version="1.0" encoding="utf-8"?>');
  lines.push('<svg xmlns="http://www.w3.org/2000/svg" viewBox="-1 -9 12 10">');
  lines.push('  <g transform="scale(1,-1)">');
  lines.push('    <line id="w-1" x1="0" y1="0" x2="10" y2="0" stroke="black" stroke-width="0.2" stroke-linecap="square" />');
  lines.push('    <line id="w-2" x1="10" y1="0" x2="10" y2="8" stroke="black" stroke-width="0.2" stroke-linecap="square" />');
  lines.push('    <line id="w-3" x1="10" y1="8" x2="0" y2="8" stroke="black" stroke-width="0.2" stroke-linecap="square" />');
  lines.push('    <line id="w-4" x1="0" y1="8" x2="0" y2="0" stroke="black" stroke-width="0.2" stroke-linecap="square" />');
  lines.push('  </g>');
  lines.push('</svg>');
  lines.push('```');
  lines.push('');
  lines.push('### lv-1/door.csv (no SVG needed)');
  lines.push('```csv');
  lines.push('id,host_id,position,material,width,height,operation');
  lines.push('d-1,w-1,0.3,wood,0.9,2.1,single_swing');
  lines.push('```');
  lines.push('');
  lines.push('### lv-1/space.csv (no SVG needed)');
  lines.push('```csv');
  lines.push('id,x,y,name');
  lines.push('sp-1,5,4,Living Room');
  lines.push('```');

  return lines.join('\n');
}
