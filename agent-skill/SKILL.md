---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

> **Version 0.1.10** (published 2026-04-01). If this date is more than 1 week ago, run `npm update -g bimdown-cli` to update, then re-download this SKILL.md from https://raw.githubusercontent.com/NovaShang/BimDown/main/agent-skill/SKILL.md

You are an AI Coder operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## Core Architecture & Base Concepts

- **Global Unit is METERS**: All coordinates, widths, and structural attributes in CSV/SVG MUST strictly use METERS. BimDown simulates real-world dimensions.
- **Computed Fields are READ-ONLY**: Any field marked with `computed: true` (or listed in `virtual_fields`) is automatically calculated by the CLI. **DO NOT** write these fields to CSV files. You can retrieve their values using `bimdown query`.
- **Dual Nature**: Properties live in `{name}.csv`. 2D geometry lives in a sibling `{name}.svg` file. The `id` fields across both must match perfectly.
- **SVG-derived virtual columns**: When you write geometry in SVG, the CLI automatically computes these fields for `bimdown query` — do NOT write them to CSV:
  - Line elements (wall, beam, pipe, etc.): `length`, `start_x`, `start_y`, `end_x`, `end_y`
  - Polygon elements (slab, roof, etc.): `area`, `perimeter`
  - All elements: `level_id` (inferred from folder name, e.g. `lv-1/` → `lv-1`)
- **Concrete Example of CSV+SVG Linked State**:
  > `lv-1/wall.csv` (note: NO `level_id` column — it is auto-inferred):
  > `id,thickness,material`
  > `w-1,0.2,concrete`
  >
  > `lv-1/wall.svg`:
  > `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -10 10 10"> <g transform="scale(1,-1)"> <path id="w-1" d="M 0 0 L 10 0" stroke-width="0.2" /> </g> </svg>`
  >
  > After this, `bimdown query . "SELECT id, length, level_id FROM wall"` returns `w-1, 10.0, lv-1` — both `length` and `level_id` are computed automatically.

## Project Directory Structure

```
project/
  project_metadata.json        # project root marker (format version, name, units)
  global/                      # global-only files — MUST be here, NOT in lv-N/
    grid.csv
    level.csv
    mesh.csv
  lv-1/                        # per-level files
    wall.csv + wall.svg        # elements with geometry have paired CSV+SVG
    door.csv                   # hosted elements are CSV-only (parametric position on host wall)
    space.csv                  # spaces are CSV-only (seed point, boundary auto-derived)
    ...
  lv-2/
    ...
```

**Key rules**:
- `level.csv`, `grid.csv`, `mesh.csv` MUST live in `global/`, never in `lv-N/` directories
- Per-level elements (wall, door, slab, space, etc.) go in `lv-N/` directories
- The folder name (e.g. `lv-1`) becomes the element's `level_id` — do NOT write `level_id` to CSV

## Recommended Workflow for Creating/Modifying Buildings

1. **Plan spatial layout first**: Before writing any files, reason through the spatial relationships — wall positions, room adjacencies, door/window placements. Sketch coordinates mentally or on paper.
2. **Write SVG geometry first**: Create the `.svg` files (walls, slabs, columns) with correct coordinates. Geometry determines everything else.
3. **Write CSV attributes second**: Create the `.csv` files with element properties (material, thickness, etc.). Remember: do NOT include computed fields like `level_id`, `length`, `area`.
4. **Render and visually verify**: Run `bimdown render <dir> -o render.png` and **view the PNG image** to confirm the layout is correct. Check that walls connect properly, rooms are enclosed, and doors/windows are in the right positions.
5. **Validate**: Run `bimdown validate <dir>` to catch any schema or reference errors.
6. **Iterate**: If the render shows problems, fix the SVG geometry and re-render until the layout looks right.

## CLI Tools & Best Practices

1. **`bimdown query <dir> <sql> --json`**: Runs DuckDB SQL across all tables, including SVG-derived virtual columns.
   - **Example**: `bimdown query ./proj "SELECT id, length FROM wall WHERE length > 5.0" --json`
2. **`bimdown render <dir> [-l level] [-o output.png] [-w width]`**: Renders a level into a PNG blueprint image (default 2048px wide). Use `.svg` extension for SVG output. **Always render after modifying geometry and view the PNG to visually verify the result.**
3. **`bimdown validate <dir>`**: Validates the project directory against schema constraints. **Run this EVERY TIME after modifying CSV or SVG files** to catch ID format, reference, and structure errors early!
4. **`bimdown schema [table]`**: Prints the full schema for any element type. Use this to look up fields before creating elements.
5. **`bimdown diff <dirA> <dirB>`**: Emits a `+`, `-`, `~` structural difference between project snapshots.
6. **`bimdown init <dir>`**: Creates a new empty BimDown project with the correct directory structure.

## Critical File & Geometry Rules

- **ID format**:
  - **Grid and Level** allow any string after prefix: level: `lv-` + any string (e.g. `lv-1`, `lv-A`, `lv-B2`); grid: `gr-` + any string (e.g. `gr-1`, `gr-A`, `gr-B2`)
  - **All other elements** use `{prefix}-{number}` (digits only): wall → `w-{n}`, column → `c-{n}`, slab → `sl-{n}`, space → `sp-{n}`, door → `d-{n}`, window → `wn-{n}`, ...
  - **Always run `bimdown validate` to confirm your IDs are compliant.**
- **SVG Coordinate Y-Flip**: All geometry inside `.svg` files **MUST** be wrapped in a Y-axis flip group: `<g transform="scale(1,-1)"> ... </g>`. This is just a fixed boilerplate — you do NOT need to do any coordinate conversion. Use normal Cartesian coordinates (X = right, Y = up) directly inside the group.
- **CSV vs Computed Fields**: Only write fields that are NOT marked as computed. Specifically, `level_id`, `length`, `area`, `start_x/y`, `end_x/y`, `perimeter`, `volume`, `bbox_*` are all auto-computed — never write them to CSV.

## Base Schema Definitions

These abstract schemas provide common fields for concrete tables below.

### Base: `element`
```yaml
name: element
abstract: true

fields:
  - name: id
    type: string
    required: true

  - name: number
    type: string

  - name: level_id
    type: reference
    reference: level
    computed: true

  - name: created_at
    type: datetime
    computed: true

  - name: updated_at
    type: datetime
    computed: true

  - name: base_offset
    type: float
    default: 0

  - name: mesh_file
    type: string
    description: Optional GLB mesh path for precise 3D visualization

  - name: volume
    type: float
    computed: true

  - name: bbox_min_x
    type: float
    computed: true

  - name: bbox_min_y
    type: float
    computed: true

  - name: bbox_min_z
    type: float
    computed: true

  - name: bbox_max_x
    type: float
    computed: true

  - name: bbox_max_y
    type: float
    computed: true

  - name: bbox_max_z
    type: float
    computed: true
```

### Base: `hosted_element`
```yaml
name: hosted_element
bases:
  - element
abstract: true

fields:
  - name: host_id
    type: reference
    reference: element
    required: true

  - name: position
    type: float
    required: true
    description: Distance in meters from the host element's start point to the center of this opening
```

### Base: `line_element`
```yaml
name: line_element
bases:
  - element
abstract: true

fields:
  - name: start_x
    type: float
    computed: true

  - name: start_y
    type: float
    computed: true

  - name: end_x
    type: float
    computed: true

  - name: end_y
    type: float
    computed: true

  - name: length
    type: float
    computed: true
```

### Base: `materialized`
```yaml
name: materialized
abstract: true

fields:
  - name: material
    type: enum
    values:
      - concrete
      - steel
      - wood
      - clt
      - glass
      - aluminum
      - brick
      - stone
      - gypsum
      - insulation
      - copper
      - pvc
      - ceramic
      - fiber_cement
      - composite
```

### Base: `point_element`
```yaml
name: point_element
bases:
  - element
abstract: true

fields:
  - name: x
    type: float
    computed: true

  - name: y
    type: float
    computed: true

  - name: rotation
    type: float
    computed: true
```

### Base: `polygon_element`
```yaml
name: polygon_element
bases:
  - element
abstract: true

fields:
  - name: points
    type: string
    computed: true
    
  - name: area
    type: float
    computed: true
```

### Base: `vertical_span`
```yaml
name: vertical_span
abstract: true

fields:
  - name: top_level_id
    type: reference
    reference: level
    description: Top constraint level. Empty = next level above current level.

  - name: top_offset
    type: float
    default: 0
    description: Offset from top level. Default 0.

  - name: height
    type: float
    computed: true
```

## Core Schema Topologies (Concrete Tables)

Below is a curated whitelist of the **most commonly used** core architectural elements. 

> **IMPORTANT**: The complete list of available elements in this project is:
> `beam`, `brace`, `cable_tray`, `ceiling`, `column`, `conduit`, `curtain_wall`, `door`, `duct`, `equipment`, `foundation`, `grid`, `level`, `mep_node`, `mesh`, `opening`, `pipe`, `railing`, `ramp`, `roof`, `room_separator`, `slab`, `space`, `stair`, `structure_column`, `structure_slab`, `structure_wall`, `terminal`, `wall`, `window`
> 
> If the user asks you to modify or generate elements not listed below, **RUN** `bimdown schema <table_name>` to fetch their requirements!

### Table: `door` (Prefix: `d`)
- **Geometry**: CSV only
- **position**: Distance in meters from the wall's start point to the center of the door. Calculate from the wall's SVG geometry coordinates.
```yaml
id_prefix: d
name: door
description: "Doors NEVER exist independently. When creating or modifying a door, you MUST ensure it is hosted on a valid wall segment. In scripts, ensure coordinates intersect the wall's line."
bases:
  - hosted_element
  - materialized
host_type: wall

fields:
  - name: width
    type: float
    required: true

  - name: height
    type: float

  - name: operation
    type: enum
    values:
      - single_swing
      - double_swing
      - sliding
      - folding
      - revolving

  - name: hinge_position
    type: enum
    values:
      - start
      - end

  - name: swing_side
    type: enum
    values:
      - left
      - right

virtual_fields: [level_id, created_at, updated_at, volume, bbox_min_x, bbox_min_y, bbox_min_z, bbox_max_x, bbox_max_y, bbox_max_z]
```

### Table: `grid` (Prefix: `gr`)
- **Geometry**: CSV only
```yaml
id_prefix: gr
name: grid

fields:
  - name: id
    type: string
    required: true

  - name: number
    type: string
    required: true

  - name: start_x
    type: float
    required: true

  - name: start_y
    type: float
    required: true

  - name: end_x
    type: float
    required: true

  - name: end_y
    type: float
    required: true
```

### Table: `level` (Prefix: `lv`)
- **Geometry**: CSV only
```yaml
id_prefix: lv
name: level

fields:
  - name: id
    type: string
    required: true

  - name: number
    type: string
    required: true

  - name: name
    type: string

  - name: elevation
    type: float
    required: true
```

### Table: `space` (Prefix: `sp`)
- **Geometry**: CSV only
```yaml
id_prefix: sp
name: space
bases:
  - element

fields:
  - name: x
    type: float
    required: true
    description: Seed point X coordinate (room interior point)

  - name: y
    type: float
    required: true
    description: Seed point Y coordinate (room interior point)

  - name: name
    type: string

virtual_fields: [level_id, created_at, updated_at, volume, bbox_min_x, bbox_min_y, bbox_min_z, bbox_max_x, bbox_max_y, bbox_max_z]
```

### Table: `wall` (Prefix: `w`)
- **Geometry**: SVG required
- **IMPORTANT**: A wall MUST be one complete straight line (start to end). Do NOT split a wall into segments for doors/windows. Doors and windows attach to the wall via the `position` parameter on the host wall.
```yaml
id_prefix: w
name: wall
bases:
  - line_element
  - vertical_span
  - materialized

fields:
  - name: thickness
    type: float
    required: true
    description: Wall thickness in meters. SVG stroke-width should match but CSV is source of truth.

virtual_fields: [level_id, created_at, updated_at, volume, bbox_min_x, bbox_min_y, bbox_min_z, bbox_max_x, bbox_max_y, bbox_max_z, start_x, start_y, end_x, end_y, length, height]
```

### Table: `window` (Prefix: `wn`)
- **Geometry**: CSV only
- **position**: Distance in meters from the wall's start point to the center of the window. Calculate from the wall's SVG geometry coordinates.
```yaml
id_prefix: wn
name: window
bases:
  - hosted_element
  - materialized
host_type: wall

fields:
  - name: width
    type: float
    required: true

  - name: height
    type: float

virtual_fields: [level_id, created_at, updated_at, volume, bbox_min_x, bbox_min_y, bbox_min_z, bbox_max_x, bbox_max_y, bbox_max_z]
```

