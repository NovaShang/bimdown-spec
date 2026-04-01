---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

You are an AI Agent operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## 🏗️ About BimDown Format

- **Architecture**: A project is split into directories. `global/` contains cross-floor elements (like grids, levels). Other folders represent specific levels (e.g. `lv-1/`).
- **Dual Nature**: Semantics and properties live in `{name}.csv` files. The 2D geometry lives in a sibling `{name}.svg` file. 
- **Synchronized Modification**: If you add/modify/remove an entity via your own Python or JS scripts, you MUST ensure both the CSV row and SVG node (sharing the exact same `id`) are updated synchronously. Do not leave zombies.

### Common Fields
All CSV tables implicitly require these universally understood fields:
- `id`: Unique string identifier (required). Must match the `id` attribute in the SVG node.
- `name`: Human readable name.
- `level_id`: Only applies to elements placed on a specific level. Maps to a `level.csv` ID.

## 🛠️ CLI Tools & Best Practices

The `bimdown` CLI is your most powerful tool. You should use it to query data instead of parsing massive CSVs yourself, and use it to validate your edits.

1. **`bimdown query <dir> <sql> --json`**: Runs DuckDB SQL across all tables. ALWAYS use this instead of writing raw regex/parsers to analyze CSV files. Example: `bimdown query . "SELECT id, thickness FROM wall WHERE level_id='lv-1'" --json`.
2. **`bimdown validate <dir>`**: Validates the project directory against schema constraints. **Run this EVERY TIME after you modify CSV or SVG files** to ensure you didn't break topological constraints!
3. **`bimdown schema [table]`**: Prints the full schema data for any specific element type. Use this when you need to know exactly what fields an obscure table requires.
4. **`bimdown diff <dirA> <dirB>`**: Emits a simple `+`, `-`, `~` structural difference between two project snapshots.
5. **`bimdown init <dir>`**: Scaffolds a fresh, empty project skeleton.

## 📐 Core Schema Topologies (Progressive Disclosure)

Below is a curated whitelist of the **most commonly used** core architectural elements and their hard constraints. 

> **IMPORTANT**: This is NOT the full list of tables! If the user asks you to modify or generate elements not listed here (like `pipe`, `duct`, `beam`, `column`, `stair`, `equipment`, etc.), **YOU MUST RUN** `bimdown schema <table_name>` to fetch the strict requirements before you write the code to modify them!

### Table: `door` (Prefix: `d`)
- **Has Geometry**: No (.csv only)
- **Topology Rule**: Must be hosted on a `wall`.
- **Core Rule**: Doors NEVER exist independently. When creating or modifying a door, you MUST ensure it is hosted on a valid wall segment. In scripts, ensure coordinates intersect the wall's line.
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `host_id`: Must reference a valid `element` ID.
  - `position`: Parametric position along host element (0.0 = start, 1.0 = end, center of opening)

### Table: `grid` (Prefix: `gr`)
- **Has Geometry**: No (.csv only)

### Table: `level` (Prefix: `lv`)
- **Has Geometry**: No (.csv only)

### Table: `space` (Prefix: `sp`)
- **Has Geometry**: No (.csv only)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `x`: Seed point X coordinate (room interior point)
  - `y`: Seed point Y coordinate (room interior point)

### Table: `wall` (Prefix: `w`)
- **Has Geometry**: Yes (.svg required)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.
  - `thickness`: Wall thickness in meters. SVG stroke-width should match but CSV is source of truth.

### Table: `window` (Prefix: `wn`)
- **Has Geometry**: No (.csv only)
- **Topology Rule**: Must be hosted on a `wall`.
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `host_id`: Must reference a valid `element` ID.
  - `position`: Parametric position along host element (0.0 = start, 1.0 = end, center of opening)

