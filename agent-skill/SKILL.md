---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

You are an AI Coder operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV for semantics and SVG for geometry.

## Core Architecture & Base Concepts

- **Global Unit is METERS**: All coordinates, widths, and structural attributes in CSV/SVG MUST strictly use METERS. BimDown simulates real-world dimensions.
- **Computed Fields are READ-ONLY**: Any field in the YAML marked with `computed: true` (or listed in `virtual_fields`) is automatically calculated by the CLI. **DO NOT** write these fields to CSV files. You can retrieve their values using `bimdown query`.
- **Dual Nature**: Properties live in `{name}.csv`. 2D geometry lives in a sibling `{name}.svg` file. The `id` fields across both must match perfectly.
- **Concrete Example of CSV+SVG Linked State**:
  > `lv-1/wall.csv`:
  > `id,name,level_id,thickness`
  > `w-1,MainWall,lv-1,0.2`
  > 
  > `lv-1/wall.svg`:
  > `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -10 10 10"> <g transform="scale(1,-1)"> <path id="w-1" d="M 0 0 L 10 0" stroke-width="0.2" /> </g> </svg>`

## CLI Tools & Best Practices

1. **`bimdown query <dir> <sql> --json`**: Runs DuckDB SQL across all tables. 
   - **Spatial SQL Tip**: The CLI extracts geometry from `.svg` and injects virtual columns (length, start_x, etc.).
   - **Example**: `bimdown query ./proj "SELECT id, length FROM wall WHERE length > 5.0" --json`
2. **`bimdown render <dir> [options]`**: Renders the BimDown project into a beautiful visual blueprint (PNG/SVG). **As a multimodal AI, you MUST use this tool to generate an image and then "view" it to visually QA your geometry modifications.**
3. **`bimdown validate <dir>`**: Validates the project directory against schema constraints. **Run this EVERY TIME after your scripts modify CSV or SVG files** to ensure you didn't break topological or ID format constraints!
4. **`bimdown schema [table]`**: Prints the full schema data for any element type.
5. **`bimdown diff <dirA> <dirB>`**: Emits a `+`, `-`, `~` structural difference between project snapshots.

## Critical File & Geometry Rules

- **Strict ID Formats**: Many elements require specific ID patterns. For example, Grids MUST be `gr-{number}` (e.g. `gr-1`), Levels MUST be `lv-{name}`. **Always run `bimdown validate` early to confirm your ID naming is compliant.**
- **SVG Coordinate Y-Flip**: All geometry inside `.svg` files **MUST** be wrapped in a Y-axis flip group: `<g transform="scale(1,-1)"> ... </g>`. This ensures the 2D SVG matches the right-handed BIM coordinate system.
- **CSV vs Inferred Fields**: Only attributes listed in the schema that are NOT marked as 'inferred' should be written to the CSV. Specifically, `level_id` is always inferred from the folder structure and MUST be omitted from CSV files.

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
    description: Parametric position along host element (0.0 = start, 1.0 = end, center of opening)
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

