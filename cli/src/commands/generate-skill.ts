import { buildRegistry, getSpecDir, TABLES_WITH_SVG } from '../schema/registry.js';
import { loadAllSchemas } from '../schema/loader.js';
import { existsSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

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

  let md = `---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

You are an AI Coder operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## Core Architecture & Base Concepts

- **Global Unit is METERS**: All coordinates, widths, and structural attributes in CSV/SVG MUST strictly use METERS. BimDown simulates real-world dimensions.
- **Computed Fields are READ-ONLY**: Any field in the YAML marked with \`computed: true\` (or listed in \`virtual_fields\`) is automatically calculated by the CLI. **DO NOT** write these fields to CSV files. You can retrieve their values using \`bimdown query\`.
- **Dual Nature**: Properties live in \`{name}.csv\`. 2D geometry lives in a sibling \`{name}.svg\` file. The \`id\` fields across both must match perfectly.
- **Concrete Example of CSV+SVG Linked State**:
  > \`lv-1/wall.csv\`:
  > \`id,name,level_id,thickness\`
  > \`w-1,MainWall,lv-1,0.2\`
  > 
  > \`lv-1/wall.svg\`:
  > \`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -10 10 10"> <g transform="scale(1,-1)"> <path id="w-1" d="M 0 0 L 10 0" stroke-width="0.2" /> </g> </svg>\`

## CLI Tools & Best Practices

1. **\`bimdown query <dir> <sql> --json\`**: Runs DuckDB SQL across all tables. 
   - **Spatial SQL Tip**: The CLI extracts geometry from \`.svg\` and injects virtual columns (length, start_x, etc.).
   - **Example**: \`bimdown query ./proj "SELECT id, length FROM wall WHERE length > 5.0" --json\`
2. **\`bimdown render <dir> [options]\`**: Renders the BimDown project into a beautiful visual blueprint (PNG/SVG). **As a multimodal AI, you MUST use this tool to generate an image and then "view" it to visually QA your geometry modifications.**
3. **\`bimdown validate <dir>\`**: Validates the project directory against schema constraints. **Run this EVERY TIME after your scripts modify CSV or SVG files** to ensure you didn't break topological or ID format constraints!
4. **\`bimdown schema [table]\`**: Prints the full schema data for any element type.
5. **\`bimdown diff <dirA> <dirB>\`**: Emits a \`+\`, \`-\`, \`~\` structural difference between project snapshots.

## Critical File & Geometry Rules

- **Strict ID Formats**: Many elements require specific ID patterns. For example, Grids MUST be \`gr-{number}\` (e.g. \`gr-1\`), Levels MUST be \`lv-{name}\`. **Always run \`bimdown validate\` early to confirm your ID naming is compliant.**
- **SVG Coordinate Y-Flip**: All geometry inside \`.svg\` files **MUST** be wrapped in a Y-axis flip group: \`<g transform="scale(1,-1)"> ... </g>\`. This ensures the 2D SVG matches the right-handed BIM coordinate system.
- **CSV vs Inferred Fields**: Only attributes listed in the schema that are NOT marked as 'inferred' should be written to the CSV. Specifically, \`level_id\` is always inferred from the folder structure and MUST be omitted from CSV files.

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
