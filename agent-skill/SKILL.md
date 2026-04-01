---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

You are an AI Coder operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## 🏗️ Core Architecture & Base Concepts

- **Global Unit is METERS**: All coordinates, widths, and structural attributes in CSV/SVG MUST strictly use METERS. BimDown simulates real-world dimensions.
- **Dual Nature**: Properties live in `{name}.csv`. 2D geometry lives in a sibling `{name}.svg` file.
- **Direct Scripting Encouraged**: You are an AI capable of writing code. You are **STRONGLY ENCOURAGED** to write and execute Python or Node.js scripts to perform bulk edits on CSV/SVG files. Just ensure that the `id` fields across CSV and SVG match perfectly and topological constraints are met!
- **Concrete Example of CSV+SVG Linked State**:
  > `lv-1/wall.csv`:
  > `id,name,level_id,thickness`
  > `w-1,MainWall,lv-1,0.2`
  > 
  > `lv-1/wall.svg`:
  > `<svg xmlns="http://www.w3.org/2000/svg">  <path id="w-1" d="M 0 0 L 10 0" stroke-width="0.2" />  </svg>`
- **Base Classes**:
  - `element`: All objects have `id`, `name`, and `level_id`.
  - `hosted_element`: Elements (like Door, Window) that CANNOT exist independently. They possess a `host_id` and must geometrically intersect their host.
  - `vertical_span`: Elements spanning heights (like walls) requiring `top_level_id` and `top_offset`.
  - `line/point/polygon_element`: Dictates whether the SVG geometry must be a linear `<path>`, a point-coordinate `<circle/rect>`, or a closed `<polygon>`.

## 🛠️ CLI Tools & Best Practices

1. **`bimdown query <dir> <sql> --json`**: Runs DuckDB SQL across all tables. **MAGIC TIP**: The CLI automatically extracts spatial geometry from the `.svg` files and injects them as virtual columns into the SQL tables! You CAN write SQL queries to filter elements by lengths, coordinates, or spatial rules even though those numbers don't explicitly exist in the CSV!
2. **`bimdown render <dir> [options]`**: Renders the BimDown project into a beautiful visual blueprint (PNG/SVG). **As a multimodal AI, you MUST use this tool to generate an image and then "view" it to visually QA your geometry modifications.**
3. **`bimdown validate <dir>`**: Validates the project directory against schema constraints. **Run this EVERY TIME after your scripts modify CSV or SVG files** to ensure you didn't break topological constraints!
4. **`bimdown schema [table]`**: Prints the full schema data for any element type.
5. **`bimdown diff <dirA> <dirB>`**: Emits a `+`, `-`, `~` structural difference between project snapshots.

## 📐 Core Schema Topologies (Progressive Disclosure)

Below is a curated whitelist of the **most commonly used** core architectural elements and their hard constraints. 

> **IMPORTANT**: This is NOT the full list of tables! 
> The complete list of available elements in this project is:
> `beam`, `brace`, `cable_tray`, `ceiling`, `column`, `conduit`, `curtain_wall`, `door`, `duct`, `equipment`, `foundation`, `grid`, `level`, `mep_node`, `mesh`, `opening`, `pipe`, `railing`, `ramp`, `roof`, `room_separator`, `slab`, `space`, `stair`, `structure_column`, `structure_slab`, `structure_wall`, `terminal`, `wall`, `window`
> 
> If the user asks you to modify or generate elements not listed below in the Core Schema, **YOU MUST RUN** `bimdown schema <table_name>` to fetch their strict requirements before you write the code to modify them!

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

