import { buildRegistry, getSpecDir, TABLES_WITH_SVG, GLOBAL_ONLY_TABLES, ID_PREFIXES } from '../schema/registry.js';
import { loadAllSchemas } from '../schema/loader.js';
import { existsSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
declare const CLI_VERSION: string;

const CORE_TABLES = new Set(['level', 'grid', 'wall', 'door', 'window', 'space']);
const BASE_SCHEMAS = new Set(['element', 'hosted_element', 'materialized', 'vertical_span', 'line_element', 'point_element', 'polygon_element']);

export function generateSkill(outputDir?: string) {
  const specDir = getSpecDir();
  const csvSchemaDir = join(specDir, 'csv-schema');
  const registry = buildRegistry(specDir);
  
  // We'll need the raw file paths for all schemas
  const schemas = loadAllSchemas(csvSchemaDir);

  const sortedNames = [...registry.keys()].sort();
  const allTableNames = sortedNames.map(t => '`' + t + '`').join(', ');

  // Build ID format reference from registry
  const FREEFORM_ID_TABLES = new Set(['grid', 'level']);
  const globalOnlyList = [...GLOBAL_ONLY_TABLES].map(t => `\`${t}.csv\``).join(', ');
  const idExamples = Object.entries(ID_PREFIXES)
    .map(([table, prefix]) => {
      const example = FREEFORM_ID_TABLES.has(table)
        ? `${table}: \`${prefix}-\` + any string (e.g. \`${prefix}-1\`, \`${prefix}-A\`, \`${prefix}-B2\`)`
        : `${table} → \`${prefix}-{n}\``;
      return example;
    });
  const freeformExamples = idExamples.filter((_, i) => FREEFORM_ID_TABLES.has(Object.keys(ID_PREFIXES)[i]));
  const numericExamples = idExamples.filter((_, i) => !FREEFORM_ID_TABLES.has(Object.keys(ID_PREFIXES)[i]));

  const today = new Date();
  const publishDate = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;

  const cliVersion = typeof CLI_VERSION !== 'undefined' ? CLI_VERSION : 'unknown';

  let md = `---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

> **Version ${cliVersion}** (published ${publishDate}). If this date is more than 1 week ago, run \`npm update -g bimdown-cli\` to update, then re-download this SKILL.md from https://raw.githubusercontent.com/NovaShang/BimDown/main/agent-skill/SKILL.md

You are an AI Coder operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## Core Architecture & Base Concepts

- **Global Unit is METERS**: All coordinates, widths, and structural attributes in CSV/SVG MUST strictly use METERS. BimDown simulates real-world dimensions.
- **Computed Fields are READ-ONLY**: Any field marked with \`computed: true\` (or listed in \`virtual_fields\`) is automatically calculated by the CLI. **DO NOT** write these fields to CSV files. You can retrieve their values using \`bimdown query\`.
- **Dual Nature**: Properties live in \`{name}.csv\`. 2D geometry lives in a sibling \`{name}.svg\` file. The \`id\` fields across both must match perfectly.
- **SVG-derived virtual columns**: When you write geometry in SVG, the CLI automatically computes these fields for \`bimdown query\` — do NOT write them to CSV:
  - Line elements (wall, beam, pipe, etc.): \`length\`, \`start_x\`, \`start_y\`, \`end_x\`, \`end_y\`
  - Polygon elements (slab, roof, etc.): \`area\`, \`perimeter\`
  - All elements: \`level_id\` (inferred from folder name, e.g. \`lv-1/\` → \`lv-1\`)
- **Concrete Example of CSV+SVG Linked State**:
  > \`lv-1/wall.csv\` (note: NO \`level_id\` column — it is auto-inferred):
  > \`id,thickness,material\`
  > \`w-1,0.2,concrete\`
  >
  > \`lv-1/wall.svg\`:
  > \`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -10 10 10"> <g transform="scale(1,-1)"> <path id="w-1" d="M 0 0 L 10 0" stroke-width="0.2" /> </g> </svg>\`
  >
  > After this, \`bimdown query . "SELECT id, length, level_id FROM wall"\` returns \`w-1, 10.0, lv-1\` — both \`length\` and \`level_id\` are computed automatically.

## Project Directory Structure

\`\`\`
project/
  project_metadata.json        # project root marker (format version, name, units)
  global/                      # global-only files — MUST be here, NOT in lv-N/
    ${[...GLOBAL_ONLY_TABLES].sort().map(t => t + '.csv').join('\n    ')}
  lv-1/                        # per-level files
    wall.csv + wall.svg        # elements with geometry have paired CSV+SVG
    door.csv                   # hosted elements are CSV-only (parametric position on host wall)
    space.csv                  # spaces are CSV-only (seed point, boundary auto-derived)
    ...
  lv-2/
    ...
\`\`\`

**Key rules**:
- ${globalOnlyList} MUST live in \`global/\`, never in \`lv-N/\` directories
- Per-level elements (wall, door, slab, space, etc.) go in \`lv-N/\` directories
- The folder name (e.g. \`lv-1\`) becomes the element's \`level_id\` — do NOT write \`level_id\` to CSV

## Recommended Workflow for Creating/Modifying Buildings

1. **Plan spatial layout first**: Before writing any files, reason through the spatial relationships — wall positions, room adjacencies, door/window placements. Sketch coordinates mentally or on paper.
2. **Write SVG geometry first**: Create the \`.svg\` files (walls, slabs, columns) with correct coordinates. Geometry determines everything else.
3. **Write CSV attributes second**: Create the \`.csv\` files with element properties (material, thickness, etc.). Remember: do NOT include computed fields like \`level_id\`, \`length\`, \`area\`.
4. **Render and visually verify**: Run \`bimdown render <dir> -o render.png\` and **view the PNG image** to confirm the layout is correct. Check that walls connect properly, rooms are enclosed, and doors/windows are in the right positions.
5. **Validate**: Run \`bimdown validate <dir>\` to catch any schema or reference errors.
6. **Iterate**: If the render shows problems, fix the SVG geometry and re-render until the layout looks right.

## CLI Tools & Best Practices

1. **\`bimdown query <dir> <sql> --json\`**: Runs DuckDB SQL across all tables, including SVG-derived virtual columns.
   - **Example**: \`bimdown query ./proj "SELECT id, length FROM wall WHERE length > 5.0" --json\`
2. **\`bimdown render <dir> [-l level] [-o output.png] [-w width]\`**: Renders a level into a PNG blueprint image (default 2048px wide). Use \`.svg\` extension for SVG output. **Always render after modifying geometry and view the PNG to visually verify the result.**
3. **\`bimdown validate <dir>\`**: Validates the project directory against schema constraints. **Run this EVERY TIME after modifying CSV or SVG files** to catch ID format, reference, and structure errors early!
4. **\`bimdown schema [table]\`**: Prints the full schema for any element type. Use this to look up fields before creating elements.
5. **\`bimdown diff <dirA> <dirB>\`**: Emits a \`+\`, \`-\`, \`~\` structural difference between project snapshots.
6. **\`bimdown init <dir>\`**: Creates a new empty BimDown project with the correct directory structure.

## Critical File & Geometry Rules

- **ID format**:
  - **Grid and Level** allow any string after prefix: ${freeformExamples.join('; ')}
  - **All other elements** use \`{prefix}-{number}\` (digits only): ${numericExamples.slice(0, 6).join(', ')}, ...
  - **Always run \`bimdown validate\` to confirm your IDs are compliant.**
- **SVG Coordinate Y-Flip**: All geometry inside \`.svg\` files **MUST** be wrapped in a Y-axis flip group: \`<g transform="scale(1,-1)"> ... </g>\`. This is just a fixed boilerplate — you do NOT need to do any coordinate conversion. Use normal Cartesian coordinates (X = right, Y = up) directly inside the group.
- **CSV vs Computed Fields**: Only write fields that are NOT marked as computed. Specifically, \`level_id\`, \`length\`, \`area\`, \`start_x/y\`, \`end_x/y\`, \`perimeter\`, \`volume\`, \`bbox_*\` are all auto-computed — never write them to CSV.

## Base Schema Definitions

These abstract schemas provide common fields for concrete tables below.

`;

  // Append Raw Base Schemas
  for (const baseName of Array.from(BASE_SCHEMAS).sort()) {
    const filePath = findSchemaFile(csvSchemaDir, baseName);
    if (filePath) {
      md += `### Base: \`${baseName}\`\n\`\`\`yaml\n${readFileSync(filePath, 'utf-8').trim()}\n\`\`\`\n\n`;
    }
  }

  md += `## Core Schema Topologies (Concrete Tables)

Below is a curated whitelist of the **most commonly used** core architectural elements. 

> **IMPORTANT**: The complete list of available elements in this project is:
> ${allTableNames}
> 
> If the user asks you to modify or generate elements not listed below, **RUN** \`bimdown schema <table_name>\` to fetch their requirements!

`;

  // Process core tables by reading their raw YAML files
  for (const name of sortedNames) {
    if (!CORE_TABLES.has(name)) continue;

    const table = registry.get(name)!;
    const hasSVG = TABLES_WITH_SVG.has(name);
    const filePath = findSchemaFile(csvSchemaDir, name);
    
    if (filePath) {
      md += `### Table: \`${table.name}\` (Prefix: \`${table.prefix}\`)\n`;
      md += `- **Geometry**: ${hasSVG ? "SVG required" : "CSV only"}\n`;
      if (name === 'wall') {
        md += `- **IMPORTANT**: A wall MUST be one complete straight line (start to end). Do NOT split a wall into segments for doors/windows. Doors and windows attach to the wall via the \`position\` parameter on the host wall.\n`;
      }
      if (name === 'door' || name === 'window') {
        md += `- **position**: Distance in meters from the wall's start point to the center of the ${name}. Calculate from the wall's SVG geometry coordinates.\n`;
      }
      md += `\`\`\`yaml\n`;
      md += `id_prefix: ${table.prefix}\n`;
      md += `${readFileSync(filePath, 'utf-8').trim()}\n`;
      
      // Also add computed fields as a hint since they aren't in the raw YAML
      const computedFields = table.allFields
        .filter(f => f.computed || f.name === 'level_id')
        .map(f => f.name);
      if (computedFields.length > 0) {
        md += `\nvirtual_fields: [${computedFields.join(', ')}]\n`;
      }
      md += `\`\`\`\n\n`;
    }
  }

  const rootDir = outputDir ? resolve(outputDir) : process.cwd();
  const dirPath = join(rootDir, 'agent-skill');
  if (!existsSync(dirPath)) mkdirSync(dirPath, { recursive: true });

  const outPath = join(dirPath, 'SKILL.md');
  writeFileSync(outPath, md, 'utf-8');
  console.log(`✅ Agent Skill documentation successfully generated at: ${outPath}`);
}

function findSchemaFile(baseDir: string, schemaName: string): string | null {
  const dirs = [baseDir, join(baseDir, 'architecture'), join(baseDir, 'mep'), join(baseDir, 'structure'), join(baseDir, 'base')];
  for (const d of dirs) {
    if (!existsSync(d)) continue;
    const p1 = join(d, `${schemaName}.yaml`);
    if (existsSync(p1)) return p1;
    const p2 = join(d, `${schemaName}.yml`);
    if (existsSync(p2)) return p2;
  }
  return null;
}
