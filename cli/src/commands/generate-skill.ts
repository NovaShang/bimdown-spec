import { buildRegistry, getSpecDir } from '../schema/registry.js';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

export function generateSkill(outputDir?: string) {
  const registry = buildRegistry(getSpecDir());

  let md = `# BimDown Agent Skill & Schema Rules

You are an AI Agent operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV and SVG.

## 🛠️ Global Directives

1. **CLI is your Toolkit**: ALWAYS use the \`bimdown query\` and \`bimdown validate\` commands to interact with data. Avoid writing raw parsers if CLI commands can fulfill the requirement.
2. **Synchronized Modification**: BimDown semantics live in CSV, geometry in SVG. If you add/modify/remove an entity in script, you MUST ensure both the CSV row and SVG node (if applicable) are handled synchronously.
3. **No Zombies**: Do not leave unhosted elements or missing references in the files. Use the schema definitions below to understand relationships.

## 📐 Schema Topologies & Constraints

The following rules are automatically derived from the core YAML schema specifications. 
Fields not listed here represent intuitive numeric/string standard attributes.

`;

  // Process tables
  const sortedNames = [...registry.keys()].sort();
  for (const name of sortedNames) {
    const table = registry.get(name)!;

    const hasTableDesc = !!table.description;
    const hasHost = !!table.hostType;
    const fieldDescs = table.allFields.filter((f) => f.description || f.reference);

    if (hasTableDesc || hasHost || fieldDescs.length > 0) {
      md += `### Table: \`${table.name}\` (Prefix: \`${table.prefix}\`)\n`;

      if (hasHost) {
        md += `- **Topology Rule**: Must be hosted on a \`${table.hostType}\`.\n`;
      }
      if (hasTableDesc) {
        md += `- **Rule**: ${table.description}\n`;
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
