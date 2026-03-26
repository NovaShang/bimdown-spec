---
name: bimdown
description: BimDown format specification and CLI tools. Covers project structure, file formats (CSV + SVG pairs), and how to use bimdown_schema/bimdown_build/bimdown_query/bimdown_info/bimdown_render tools to inspect, validate, and visualize projects.
---

# BimDown Format

BimDown is an AI-native building data format using paired CSV (attributes) and SVG (geometry) files.

## Project Structure

All model files live inside the `model/` subdirectory of your workspace. **Always write files under `model/`.**

```
model/
  global/
    level.csv          # Required — building levels
    grid.csv           # Required — structural grids (can be empty header-only)
  lv-1/                # Directory name = level id from level.csv
    wall.csv + wall.svg
    door.csv + door.svg
    column.csv + column.svg
    slab.csv + slab.svg
    space.csv + space.svg
    window.csv + window.svg
  lv-2/
    ...
```

## CRITICAL Rules (violations cause validation failure)

1. **ALL values are in METERS — both SVG coordinates AND CSV fields.** This applies to EVERYTHING: SVG geometry (`x1`, `y1`, `stroke-width`), AND CSV fields (`elevation`, `top_offset`, `base_offset`, `width`, `height`, `thickness`). A floor-to-floor height is `3.5`, NOT `3500`. A door width is `0.9`, NOT `900`. Typical ranges: elevation 0-50m, wall thickness 0.1-0.3m, door width 0.8-1.8m, room size 3-8m.
2. **CSV and SVG use the SAME file name** (both singular): `wall.csv` + `wall.svg`, `door.csv` + `door.svg`, etc.
3. **IDs are globally unique across ALL levels.** `w-1` can only exist once in the entire project. If lv-1 has `w-1` through `w-10`, lv-2 must start at `w-11`. Same for all element types.
4. **CSV and SVG are paired by `id`**: Every element has a unique id in both its CSV row and as the `id` attribute on the SVG element.
5. **IDs use prefix-number format**: `w-1`, `d-1`, `c-1`, `sl-1`, `sp-1`, `wn-1`, `lv-1`, `gr-1`.
6. **Level directories match level IDs**: If level.csv has `lv-1`, the directory is `lv-1/`.
7. **Every SVG must have `<g transform="scale(1,-1)">`** wrapper so Y-axis points up.

## Tools

You have 5 BimDown tools available. Use them — don't try to parse files manually:

- **`bimdown_schema`** — Look up exact column names and types before writing CSV files. Use `bimdown_schema wall` for one table, or `bimdown_schema` for all.
- **`bimdown_build`** — Build: validates files and syncs to cloud. **Call after writing each CSV+SVG pair** (like compiling after each module). The user only sees your work after a successful build.
- **`bimdown_info`** — Get a project overview: levels, element counts per level, totals.
- **`bimdown_query`** — Run SQL (DuckDB) on project data. Tables match CSV names. Each has a `_partition` column (`global`, `lv-1`, etc.).
- **`bimdown_render`** — Render a floor plan of a level as a PNG image. Use after creating or modifying geometry to visually verify walls, doors, windows, and spaces look correct. Pass `level` param (e.g. `"lv-1"`). If the render shows overlapping or misaligned elements, fix and render again. **This is for your internal verification only — never share the image with the user.**

## Global Tables (inline schema)

### level (prefix: lv) — `global/level.csv`

```csv
id,number,name,elevation
lv-1,1,Ground Floor,0
lv-2,2,First Floor,3.5
```

| Column | Type | Required | Notes |
|--------|------|----------|-------|
| id | string | yes | Format: `lv-{n}` |
| number | string | yes | Floor number |
| name | string | no | Display name |
| elevation | float | yes | Meters above origin |

No SVG file for levels.

### grid (prefix: gr) — `global/grid.csv`

```csv
id,number,start_x,start_y,end_x,end_y
gr-1,A,0,0,0,20
gr-2,B,6,0,6,20
```

| Column | Type | Required | Notes |
|--------|------|----------|-------|
| id | string | yes | Format: `gr-{n}` |
| number | string | yes | Grid label (A, B, 1, 2...) |
| start_x/start_y | float | yes | Start point in meters |
| end_x/end_y | float | yes | End point in meters |

No SVG file for grids.

## Architecture Tables

For the exact column definitions of these tables, use `bimdown_schema <table>`:

| Table | Prefix | SVG File | SVG Element | Host | Key Fields |
|-------|--------|----------|-------------|------|------------|
| wall | w | wall.svg | `<line>` (centerline) | — | material, thickness (from SVG stroke-width) |
| door | d | door.svg | `<line>` + `data-host` | wall | host_id, width, height, operation, hinge_position, swing_side |
| window | wn | window.svg | `<line>` + `data-host` | wall | host_id, width, height |
| column | c | column.svg | `<rect>` or `<circle>` | — | shape (rect/round), size_x, size_y |
| slab | sl | slab.svg | `<polygon>` | — | function (floor/roof/finish), thickness |
| space | sp | space.svg | `<polygon>` | — | name |
| stair | st | stair.svg | `<line>` | — | width, step_count, top_level_id |

## SVG Format

Every SVG file follows this template:

```xml
<?xml version="1.0" encoding="utf-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="{min_x} {min_y} {width} {height}">
  <g transform="scale(1,-1)">
    <!-- elements here -->
  </g>
</svg>
```

**viewBox**: Compute from all geometry coordinates, then add 1m padding on each side. Because of `scale(1,-1)`, the viewBox y is negated: if geometry spans y=0 to y=10, viewBox y = -(10+1) = -11.

**Allowed SVG elements**: `line`, `rect`, `polygon`, `circle`, `text`. Forbidden: `path`, `defs`, `use`, `script`, gradients, `filter`.

### SVG geometry by element type

**Walls** — `<line>` centerline:
```xml
<line id="w-1" x1="0" y1="0" x2="10" y2="0" stroke="black" stroke-width="0.2" stroke-linecap="square" />
```
`stroke-width` = wall thickness.

**Doors/Windows** — `<line>` on host wall with `data-host`:
```xml
<line id="d-1" data-host="w-1" x1="2" y1="0" x2="2.9" y2="0" stroke="brown" stroke-width="0.08" />
```
Position is along the host wall's centerline. The line segment represents the opening width.

**Columns** — `<rect>` or `<circle>`:
```xml
<rect id="c-1" x="4.8" y="4.8" width="0.4" height="0.4" fill="gray" />
<circle id="c-2" cx="10" cy="5" r="0.2" fill="gray" />
```

**Slabs/Spaces** — `<polygon>`:
```xml
<polygon id="sl-1" points="0,0 10,0 10,8 0,8" fill="lightgray" stroke="black" stroke-width="0.05" />
<polygon id="sp-1" points="0,0 10,0 10,8 0,8" fill="rgba(0,0,255,0.1)" stroke="blue" stroke-width="0.05" />
```

## ID Prefix Reference

| Table | Prefix | Table | Prefix |
|-------|--------|-------|--------|
| level | lv | door | d |
| grid | gr | window | wn |
| wall | w | stair | st |
| column | c | structure_wall | sw |
| slab | sl | structure_column | sc |
| space | sp | beam | bm |
