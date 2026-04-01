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
3. **`bimdown validate <dir>`**: Validates the project directory against schema constraints. **Run this EVERY TIME after your scripts modify CSV or SVG files** to ensure you didn't break topological or ID format constraints!
4. **`bimdown schema [table]`**: Prints the full schema data for any element type.
5. **`bimdown diff <dirA> <dirB>`**: Emits a `+`, `-`, `~` structural difference between project snapshots.

## 📏 Critical File & Geometry Rules

- **Strict ID Formats**: Many elements require specific ID patterns. For example, Grids MUST be `gr-{number}` (e.g. `gr-1`), Levels MUST be `lv-{name}`. **Always run `bimdown validate` early to confirm your ID naming is compliant.**
- **SVG Coordinate Y-Flip**: All geometry inside `.svg` files **MUST** be wrapped in a Y-axis flip group: `<g transform="scale(1,-1)"> ... </g>`. This ensures the 2D SVG matches the right-handed BIM coordinate system.
- **CSV vs Inferred Fields**: Only attributes listed in the schema that are NOT marked as 'inferred' should be written to the CSV. Specifically, `level_id` is always inferred from the folder structure and MUST be omitted from CSV files.
- **Global Unit is METERS**: All coordinates, widths, and structural attributes in CSV/SVG MUST strictly use METERS. BimDown simulates real-world dimensions.

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
- **CSV Columns (The only fields you write to file)**:
  - `id`: **[REQUIRED]** 
  - `number`: [OPTIONAL] 
  - `base_offset`: [OPTIONAL] 
  - `mesh_file`: [OPTIONAL] Optional GLB mesh path for precise 3D visualization
  - `host_id`: **[REQUIRED]** (Ref: `element`)
  - `position`: **[REQUIRED]** Parametric position along host element (0.0 = start, 1.0 = end, center of opening)
  - `material`: [OPTIONAL] 
  - `width`: **[REQUIRED]** 
  - `height`: [OPTIONAL] 
  - `operation`: [OPTIONAL] 
  - `hinge_position`: [OPTIONAL] 
  - `swing_side`: [OPTIONAL] 
- **Virtual Query Fields (Read-only via `bimdown query`, DO NOT write to CSV)**:
  - `level_id`: 
  - `created_at`: 
  - `updated_at`: 
  - `volume`: 
  - `bbox_min_x`: 
  - `bbox_min_y`: 
  - `bbox_min_z`: 
  - `bbox_max_x`: 
  - `bbox_max_y`: 
  - `bbox_max_z`: 

### Table: `grid` (Prefix: `gr`)
- **Has Geometry**: No (.csv only)
- **CSV Columns (The only fields you write to file)**:
  - `id`: **[REQUIRED]** 
  - `number`: **[REQUIRED]** 
  - `start_x`: **[REQUIRED]** 
  - `start_y`: **[REQUIRED]** 
  - `end_x`: **[REQUIRED]** 
  - `end_y`: **[REQUIRED]** 

### Table: `level` (Prefix: `lv`)
- **Has Geometry**: No (.csv only)
- **CSV Columns (The only fields you write to file)**:
  - `id`: **[REQUIRED]** 
  - `number`: **[REQUIRED]** 
  - `name`: [OPTIONAL] 
  - `elevation`: **[REQUIRED]** 

### Table: `space` (Prefix: `sp`)
- **Has Geometry**: No (.csv only)
- **CSV Columns (The only fields you write to file)**:
  - `id`: **[REQUIRED]** 
  - `number`: [OPTIONAL] 
  - `base_offset`: [OPTIONAL] 
  - `mesh_file`: [OPTIONAL] Optional GLB mesh path for precise 3D visualization
  - `x`: **[REQUIRED]** Seed point X coordinate (room interior point)
  - `y`: **[REQUIRED]** Seed point Y coordinate (room interior point)
  - `name`: [OPTIONAL] 
- **Virtual Query Fields (Read-only via `bimdown query`, DO NOT write to CSV)**:
  - `level_id`: 
  - `created_at`: 
  - `updated_at`: 
  - `volume`: 
  - `bbox_min_x`: 
  - `bbox_min_y`: 
  - `bbox_min_z`: 
  - `bbox_max_x`: 
  - `bbox_max_y`: 
  - `bbox_max_z`: 

### Table: `wall` (Prefix: `w`)
- **Has Geometry**: Yes (.svg required)
- **CSV Columns (The only fields you write to file)**:
  - `id`: **[REQUIRED]** 
  - `number`: [OPTIONAL] 
  - `base_offset`: [OPTIONAL] 
  - `mesh_file`: [OPTIONAL] Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: [OPTIONAL] (Ref: `level`) Top constraint level. Empty = next level above current level.
  - `top_offset`: [OPTIONAL] Offset from top level. Default 0.
  - `material`: [OPTIONAL] 
  - `thickness`: **[REQUIRED]** Wall thickness in meters. SVG stroke-width should match but CSV is source of truth.
- **Virtual Query Fields (Read-only via `bimdown query`, DO NOT write to CSV)**:
  - `level_id`: 
  - `created_at`: 
  - `updated_at`: 
  - `volume`: 
  - `bbox_min_x`: 
  - `bbox_min_y`: 
  - `bbox_min_z`: 
  - `bbox_max_x`: 
  - `bbox_max_y`: 
  - `bbox_max_z`: 
  - `start_x`: 
  - `start_y`: 
  - `end_x`: 
  - `end_y`: 
  - `length`: 
  - `height`: 

### Table: `window` (Prefix: `wn`)
- **Has Geometry**: No (.csv only)
- **Topology Rule**: Must be hosted on a `wall`.
- **CSV Columns (The only fields you write to file)**:
  - `id`: **[REQUIRED]** 
  - `number`: [OPTIONAL] 
  - `base_offset`: [OPTIONAL] 
  - `mesh_file`: [OPTIONAL] Optional GLB mesh path for precise 3D visualization
  - `host_id`: **[REQUIRED]** (Ref: `element`)
  - `position`: **[REQUIRED]** Parametric position along host element (0.0 = start, 1.0 = end, center of opening)
  - `material`: [OPTIONAL] 
  - `width`: **[REQUIRED]** 
  - `height`: [OPTIONAL] 
- **Virtual Query Fields (Read-only via `bimdown query`, DO NOT write to CSV)**:
  - `level_id`: 
  - `created_at`: 
  - `updated_at`: 
  - `volume`: 
  - `bbox_min_x`: 
  - `bbox_min_y`: 
  - `bbox_min_z`: 
  - `bbox_max_x`: 
  - `bbox_max_y`: 
  - `bbox_max_z`: 

