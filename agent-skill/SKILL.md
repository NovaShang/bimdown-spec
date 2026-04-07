---
name: bimdown
version: 1.0.1
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

## Setup / Prerequisites
Before executing any `bimdown` commands, ensure the CLI is installed globally:
```bash
npm install -g bimdown-cli
```

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
    space.csv                  # spaces: CSV seed point + space.svg boundary (computed by build)
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
4. **Render and visually verify**: Run `bimdown render <dir> -o render.png` and **view the PNG image** to confirm the layout is correct. Check that walls connect properly, rooms are enclosed, and doors/windows are in the right positions. **Save render outputs and any other non-BimDown files OUTSIDE the project directory** — the project directory must only contain BimDown CSV/SVG files, otherwise `build` will reject them.
5. **Build**: Run `bimdown build <dir>` to validate schema, check geometry, and compute space boundaries (generates `space.svg` from seed points).
6. **Iterate**: If the render or build shows problems, fix the SVG geometry and re-render until the layout looks right.
7. **Publish**: Run `bimdown publish <dir>` to upload the project and get a shareable preview URL for the user to view the 3D model in their browser.

## Reference SOPs

Before starting any building design or modeling task, **always read the relevant reference SOP**:

- **Designing a building from scratch** (from a user brief or requirements): Read [`references/building-design.md`](https://raw.githubusercontent.com/NovaShang/BimDown/main/agent-skill/references/building-design.md) for the full design-to-BIM workflow — from massing through MEP.
- **Modeling from existing plans** (floor plan images, sketches, or known dimensions): Read [`references/bim-modeling.md`](https://raw.githubusercontent.com/NovaShang/BimDown/main/agent-skill/references/bim-modeling.md) for element creation order, dependencies, and best practices.

These are step-by-step standard operating procedures. Read the relevant one **before writing any files**.

## CLI Tools & Best Practices

1. **`bimdown query <dir> <sql> --json`**: Runs DuckDB SQL across all tables, including SVG-derived virtual columns.
   - **Example**: `bimdown query ./proj "SELECT id, length FROM wall WHERE length > 5.0" --json`
2. **`bimdown render <dir> [-l level] [-o output.png] [-w width]`**: Renders a level into a PNG blueprint image (default 2048px wide). Use `.svg` extension for SVG output. **Always render after modifying geometry and view the PNG to visually verify the result.**
3. **`bimdown build <dir>`**: Validates the project, checks geometry (wall connectivity, hosted element bounds), and computes space boundaries (generates `space.svg`). **Run this EVERY TIME after modifying CSV or SVG files!** Also available as `bimdown validate` (alias).
4. **`bimdown schema [table]`**: Prints the full schema for any element type. Use this to look up fields before creating elements.
5. **`bimdown diff <dirA> <dirB>`**: Emits a `+`, `-`, `~` structural difference between project snapshots.
6. **`bimdown init <dir>`**: Creates a new empty BimDown project with the correct directory structure.
7. **`bimdown publish <dir> [--expires 7d]`**: Publishes the project to BimClaw and returns a shareable preview URL. Use this to let users view the model in a 3D editor. **Always publish after completing a model so users can preview it.**
8. **`bimdown info <dir>`**: Prints project summary (levels, element counts).
9. **`bimdown resolve-topology <dir>`**: Auto-detects coincident endpoints for MEP curves, generates `mep_nodes`, and fills connectivity fields.
10. **`bimdown merge <dirs...> -o <output>`**: Merges multiple project directories into one, resolving ID conflicts.
11. **`bimdown sync <dir>`**: Hydrates into DuckDB and dehydrates back out to CSV/SVG, applying computed defaults.

## Critical File & Geometry Rules

- **ID format**:
  - **Grid and Level** allow any string after prefix: level: `lv-` + any string (e.g. `lv-1`, `lv-A`, `lv-B2`); grid: `gr-` + any string (e.g. `gr-1`, `gr-A`, `gr-B2`)
  - **All other elements** use `{prefix}-{number}` (digits only): wall → `w-{n}`, column → `c-{n}`, slab → `sl-{n}`, space → `sp-{n}`, door → `d-{n}`, window → `wn-{n}`, ...
  - **Always run `bimdown build` to confirm your IDs are compliant.**
- **SVG Coordinate Y-Flip**: All geometry inside `.svg` files **MUST** be wrapped in a Y-axis flip group: `<g transform="scale(1,-1)"> ... </g>`. This is just a fixed boilerplate — you do NOT need to do any coordinate conversion. Use normal Cartesian coordinates (X = right, Y = up) directly inside the group.
- **CSV vs Computed Fields**: Only write fields that are NOT marked as computed. Specifically, `level_id`, `length`, `area`, `start_x/y`, `end_x/y`, `perimeter`, `volume`, `bbox_*` are all auto-computed — never write them to CSV.
- **Vertical positioning** (walls, columns, and other vertical elements):
  - `level_id`: auto-inferred from folder name — do NOT write to CSV
  - `base_offset`: vertical offset in meters from the element's level. Default 0. Usually leave empty.
  - `top_level_id`: the level where the element's top is constrained. **Leave empty** to default to the next level above. Only set this if the element spans to a non-adjacent level.
  - `top_offset`: vertical offset in meters from the top level. Default 0. Usually leave empty.
  - `height`: auto-computed from level elevations and offsets — do NOT write to CSV.
  - **For most single-story walls**: leave `top_level_id`, `top_offset`, and `base_offset` all empty — the CLI will compute the correct height from level elevations.

## Generation Tips

### Typical Values (meters)
| Element | Field | Typical Range |
|---------|-------|--------------|
| Wall (partition) | thickness | 0.1 – 0.15 |
| Wall (exterior) | thickness | 0.2 – 0.3 |
| Wall (structural) | thickness | 0.3 – 0.6 |
| Door (single) | width × height | 0.9 × 2.1 |
| Door (double) | width × height | 1.8 × 2.1 |
| Window | width × height | 1.2–1.8 × 1.5 |
| Window | base_offset (sill height) | 0.9 (standard), 0.0 (floor-to-ceiling) |
| Column | size_x × size_y | 0.3–0.6 × 0.3–0.6 |
| Slab | thickness | 0.15 – 0.25 |
| Level spacing | elevation diff | 3.0 – 4.0 |

### Room Boundary Connectivity
Rooms are enclosed by **walls, curtain walls, columns, and room separators**. For the boundary to close properly:
- Line element endpoints (walls, curtain walls, room separators) must meet exactly at shared coordinates
- Example: w-1 ends at (10,0) → w-2 must start at (10,0) for an L-junction
- The CLI `build` command warns about unconnected endpoints and computes space boundaries from closed loops

### Door/Window Placement Rules
- `position` = distance in meters from wall **start point** (the M coordinate in SVG path) to the opening **center**
- Must satisfy: `position - width/2 >= 0` AND `position + width/2 <= wall_length`
- Multiple openings on the same wall must not overlap
- The CLI `build` command warns about out-of-bounds and overlapping placements

### SVG File Template
Always use this structure for SVG files:
```xml
<svg xmlns="http://www.w3.org/2000/svg">
  <g transform="scale(1,-1)">
    <!-- elements here, using normal Cartesian coordinates (X=right, Y=up) -->
  </g>
</svg>
```

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
- **Geometry**: SVG required
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

  - name: boundary_points
    type: string
    computed: true
    description: Space boundary polygon vertices (computed by build from surrounding walls)

  - name: area
    type: float
    computed: true
    description: Space area in square meters (computed from boundary polygon)

virtual_fields: [level_id, created_at, updated_at, volume, bbox_min_x, bbox_min_y, bbox_min_z, bbox_max_x, bbox_max_y, bbox_max_z, boundary_points, area]
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

