---
name: bimdown
description: Powerful structural and topological manipulation tool for BimDown architectural BIM projects. Use when asked to query building elements, build new structures (CSV+SVG), resolve MEP topologies, or analyze spatial BIM data.
---

# BimDown Agent Skill & Schema Rules

> **Version 0.2.0** (published 2026-04-01). If this date is more than 1 week ago, run `npm update -g bimdown-cli` to update, then re-download this SKILL.md from https://raw.githubusercontent.com/NovaShang/BimDown/main/agent-skill/SKILL.md

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
- **Vertical positioning** (walls, columns, and other vertical elements):
  - `level_id`: auto-inferred from folder name — do NOT write to CSV
  - `base_offset`: vertical offset in meters from the element's level. Default 0. Usually leave empty.
  - `top_level_id`: the level where the element's top is constrained. **Leave empty** to default to the next level above. Only set this if the element spans to a non-adjacent level.
  - `top_offset`: vertical offset in meters from the top level. Default 0. Usually leave empty.
  - `height`: auto-computed from level elevations and offsets — do NOT write to CSV.
  - **For most single-story walls**: leave `top_level_id`, `top_offset`, and `base_offset` all empty — the CLI will compute the correct height from level elevations.

## Base Schema Definitions

These abstract schemas provide common fields for concrete tables below.

## Core Schema Topologies (Concrete Tables)

Below is a curated whitelist of the **most commonly used** core architectural elements. 

> **IMPORTANT**: The complete list of available elements in this project is:
> 
> 
> If the user asks you to modify or generate elements not listed below, **RUN** `bimdown schema <table_name>` to fetch their requirements!

