import { buildRegistry, getSpecDir, TABLES_WITH_SVG } from '../schema/registry.js';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const CORE_TABLES = new Set(['level', 'grid', 'wall', 'door', 'window', 'space']);

export function generateSkill(outputDir?: string) {
  const registry = buildRegistry(getSpecDir());

  let md = `---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

You are an AI Agent operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## 🏗️ About BimDown Format

- **Architecture**: A project is split into directories. \`global/\` contains cross-floor elements (like grids, levels). Other folders represent specific levels (e.g. \`lv-1/\`).
- **Dual Nature**: Semantics and properties live in \`{name}.csv\` files. The 2D geometry lives in a sibling \`{name}.svg\` file. 
- **Synchronized Modification**: If you add/modify/remove an entity via your own Python or JS scripts, you MUST ensure both the CSV row and SVG node (sharing the exact same \`id\`) are updated synchronously. Do not leave zombies.

### Common Fields
All CSV tables implicitly require these universally understood fields:
- \`id\`: Unique string identifier (required). Must match the \`id\` attribute in the SVG node.
- \`name\`: Human readable name.
- \`level_id\`: Only applies to elements placed on a specific level. Maps to a \`level.csv\` ID.

## 🛠️ CLI Tools & Best Practices

The \`bimdown\` CLI is your most powerful tool. You should use it to query data instead of parsing massive CSVs yourself, and use it to validate your edits.

1. **\`bimdown query <dir> <sql> --json\`**: Runs DuckDB SQL across all tables. ALWAYS use this instead of writing raw regex/parsers to analyze CSV files. Example: \`bimdown query . "SELECT id, thickness FROM wall WHERE level_id='lv-1'" --json\`.
2. **\`bimdown validate <dir>\`**: Validates the project directory against schema constraints. **Run this EVERY TIME after you modify CSV or SVG files** to ensure you didn't break topological constraints!
3. **\`bimdown schema [table]\`**: Prints the full schema data for any specific element type. Use this when you need to know exactly what fields an obscure table requires.
4. **\`bimdown diff <dirA> <dirB>\`**: Emits a simple \`+\`, \`-\`, \`~\` structural difference between two project snapshots.
5. **\`bimdown init <dir>\`**: Scaffolds a fresh, empty project skeleton.

## 📐 Core Schema Topologies (Progressive Disclosure)

Below is a curated whitelist of the **most commonly used** core architectural elements and their hard constraints. 

> **IMPORTANT**: This is NOT the full list of tables! If the user asks you to modify or generate elements not listed here (like \`pipe\`, \`duct\`, \`beam\`, \`column\`, \`stair\`, \`equipment\`, etc.), **YOU MUST RUN** \`bimdown schema <table_name>\` to fetch the strict requirements before you write the code to modify them!

`;

  // Process core tables only
  const sortedNames = [...registry.keys()].sort();
  for (const name of sortedNames) {
    if (!CORE_TABLES.has(name)) continue;

    const table = registry.get(name)!;
    const hasTableDesc = !!table.description;
    const hasHost = !!table.hostType;
    const hasSVG = TABLES_WITH_SVG.has(name);
    
    // Only extract constraints that are heavily domain specific (references or hand-written descriptions)
    const fieldDescs = table.allFields.filter((f) => f.description || f.reference);

    md += `### Table: \`${table.name}\` (Prefix: \`${table.prefix}\`)\n`;
    md += `- **Has Geometry**: ${hasSVG ? "Yes (.svg required)" : "No (.csv only)"}\n`;

    if (hasHost) {
      md += `- **Topology Rule**: Must be hosted on a \`${table.hostType}\`.\n`;
    }
    if (hasTableDesc) {
      md += `- **Core Rule**: ${table.description}\n`;
    }

    if (fieldDescs.length > 0) {
      md += `- **Field Constraints**:\n`;
      for (const f of fieldDescs) {
        let constraint = '';
        if (f.reference) {
          constraint += `Must reference a valid \`${f.reference}\` ID. `;
        }
        if (f.description) {
          constraint += f.description;
        }
        md += `  - \`${f.name}\`: ${constraint.trim()}\n`;
      }
    }
    md += '\n';
  }

  // Determine output path. Default is ../agent-skill/SKILL.md from current working directory
  const rootDir = outputDir ? resolve(outputDir) : process.cwd();
  const dirPath = join(rootDir, 'agent-skill');
  
  if (!existsSync(dirPath)) {
    mkdirSync(dirPath, { recursive: true });
  }

  const outPath = join(dirPath, 'SKILL.md');
  writeFileSync(outPath, md, 'utf-8');

  console.log(`✅ Agent Skill documentation successfully generated at: ${outPath}`);
}
