# BIMDown Spec

This directory defines the **CSV schema** for BIMDown — the canonical data model for a "Minimum Viable Building".

---

## Schema Overview

### Global Tables

| Table   | Description                                                   |
|---------|---------------------------------------------------------------|
| `level` | Defines floor levels by elevation. All elements reference a level. |
| `grid`  | Defines structural/reference grid lines.                      |

### Architecture

| Table    | Geometry      | Description                             |
|----------|---------------|-----------------------------------------|
| `wall`   | Line (2D)     | Architectural wall with thickness and vertical span. |
| `column` | Point         | Architectural column with section profile and vertical span. |
| `slab`   | Polygon       | Floor/roof/finish slab.                 |
| `space`  | Polygon       | Named space (room/zone).                |
| `door`   | Hosted on wall | Door with operation type.              |
| `window` | Hosted on wall | Window with dimensions.                |
| `stair`  | Spatial line (3D) | Stair with rise, run and vertical span. |

### Structure

| Table                | Geometry          | Description                            |
|----------------------|-------------------|----------------------------------------|
| `structure_wall`     | Line (2D)         | Structural wall (independent of arch). |
| `structure_column`   | Point             | Structural column with structural section profile. |
| `structure_slab`     | Polygon           | Structural slab (floor/roof only).     |
| `beam`               | Spatial line (3D) | Structural beam.                        |
| `brace`              | Spatial line (3D) | Structural brace.                       |
| `isolated_foundation`| Point             | Pad/isolated footing.                   |
| `strip_foundation`   | Line (2D)         | Strip/continuous footing.               |
| `raft_foundation`    | Polygon           | Raft/mat foundation.                    |

### MEP

| Table        | Geometry          | Description                            |
|--------------|-------------------|----------------------------------------|
| `duct`       | Spatial line (3D) | HVAC duct with section and connectivity. |
| `pipe`       | Spatial line (3D) | Plumbing/process pipe with connectivity. |
| `cable_tray` | Spatial line (3D) | Electrical cable tray.                  |
| `conduit`    | Spatial line (3D) | Electrical conduit.                     |
| `equipment`  | Point             | MEP equipment (AHU, chiller, pump…).    |
| `terminal`   | Point             | MEP terminal device (diffuser, outlet…).|

---

## Schema Field Metadata (`required` vs `computed`)

The YAML schemas use two critical tags to define field behavior across the CSV and SVG layers:
- **`required: true`**: These are absolute semantic **Sources of Truth**. They *must* be physically written in the `.csv` file by the LLM (e.g., a door's `width` or a wall's `material`). If the SVG geometry visually contradicts a `required` field (like drawing a 0.8m line for a 0.9m door), the CLI tool uses this CSV field to **auto-correct** the SVG upon sync.
- **`computed: true`**: These are spatial derivatives or pure geometry fields (e.g., `start_x`, `length`, `thickness`, `bbox`). They are **NOT** physically stored in the `.csv` file. Instead, the DuckDB CLI engine dynamically extracts them from the `.svg` ("Eyes") layer and injects them into the SQL runtime as virtual columns.

---

## Base Mixins

| Mixin                     | Key Fields                                        |
|---------------------------|---------------------------------------------------|
| `element`                 | `id` (short ID, PK), `number`                      |
| `line_element`            | `start_x/y`, `end_x/y`                            |
| `spatial_line_element`    | extends `line_element` + `start_z`, `end_z`       |
| `point_element`           | `x`, `y`, `rotation`                              |
| `polygon_element`         | `points` (serialized polygon)                     |
| `hosted_element`          | `host_id` (reference to host element), `location_param` |
| `vertical_span`           | `top_level_id`, `top_offset`                      |
| `materialized`            | `material` (enum: concrete, concrete_precast, steel, aluminum, glass, wood, brick, gypsum, metal_panel, insulation, stone, ceramic, copper, pvc, galvanized_steel) |
| `section_profile`         | `shape` (rect/round), `size_x`, `size_y`          |
| `structural_section_profile` | `shape` (rect/round/l_shape/t_shape/…), `size_x`, `size_y` |
| `mep_system`              | `system_type` (enum)                              |
| `mep_connected_segment`   | `start_node_id`, `end_node_id` (connectivity)     |

---

## Design Decisions

- **Architecture vs Structure**: Architecture and structural elements are **decoupled**. Structural elements (`structure_column`, `structure_wall`, `structure_slab`) inherit directly from geometry base mixins, not from architecture classes. This prevents cross-discipline field conflicts.
- **`section_profile` vs `structural_section_profile`**: Architecture columns use the simpler `section_profile` (rect/round). Structural elements use `structural_section_profile` which includes engineering shapes (I, T, L…).
- **Stairs use `spatial_line_element`**: Stairs are 3D elements — they have a bottom landing point and a top landing point at different elevations, requiring `start_z`/`end_z`.
- **`id` is prefixed short ID**: All tables include a required `id` field as the primary key, using the format `{prefix}-{n}` (e.g., `w-1`, `lv-3`, `d-12`). Each table has a unique prefix, and counters are 1-based per table. This format is compact (saving tokens), human-readable, and LLM-friendly — models can trivially generate valid new IDs. Round-trip fidelity is maintained via a `BimDown_Id` shared parameter stored on each Revit element. See the full prefix table below.

#### ID Prefix Table

| Table | Prefix | Example |
|---|---|---|
| `level` | `lv` | `lv-1` |
| `grid` | `gr` | `gr-1` |
| `wall` | `w` | `w-1` |
| `column` | `c` | `c-1` |
| `slab` | `sl` | `sl-1` |
| `space` | `sp` | `sp-1` |
| `door` | `d` | `d-1` |
| `window` | `wn` | `wn-1` |
| `stair` | `st` | `st-1` |
| `structure_wall` | `sw` | `sw-1` |
| `structure_column` | `sc` | `sc-1` |
| `structure_slab` | `ss` | `ss-1` |
| `beam` | `bm` | `bm-1` |
| `brace` | `br` | `br-1` |
| `isolated_foundation` | `if` | `if-1` |
| `strip_foundation` | `sf` | `sf-1` |
| `raft_foundation` | `rf` | `rf-1` |
| `duct` | `du` | `du-1` |
| `pipe` | `pi` | `pi-1` |
| `cable_tray` | `ct` | `ct-1` |
| `conduit` | `co` | `co-1` |
| `equipment` | `eq` | `eq-1` |
| `terminal` | `tm` | `tm-1` |
- **`mep_system.system_type` is enum**: Controlled vocabulary prevents free-form strings and enables reliable filtering.

---

## Storage & Agentic Architecture

To balance **pure-text LLM generation capabilities** and **robust relational queries**, BIMDown employs a dual-read strategy for CSV storage via an intelligent CLI tool/proxy:

### 1. Physical Storage (Partitioned by Level & Global)
CSVs are physically partitioned by level into small, bounded files (e.g., `1F/wall.csv`, `2F/door.csv`). However, **cross-floor elements** (e.g., `stair`, MEP `pipe` networks, high-rise `structure_column`) must NOT be forcibly anchored to a single floor. They are placed in a dedicated `global/` directory (e.g., `global/stair.csv`).
- **Why?** This is tailored for Agentic AI "from-scratch" generative design. Generating or editing a 5000-line monolithic CSV directly pushes context window limits and introduces high hallucination risks. By isolating each floor into a tiny file, Agents can read, generate, or overwrite a specific bounded local context flawlessly. Meanwhile, the `global/` folder preserves continuous topologies for entire-building systems (like piping or circulation), avoiding scattered graph logic.

### 2. Logical View & Schema Hydration (In-Memory DuckDB)
When an Agent needs to manipulate data relationally or handle complex spatial logic, it interfaces with a Backend CLI Tool wrapping an in-memory **DuckDB** instance.

The DuckDB Engine dynamically handles:
1. **Partition Unioning**: Merging `1F/wall.csv` + `2F/wall.csv` + `global/wall.csv` into a single view.
2. **Geometry Hydration**: Parsing `.svg` files to append `computed: true` spatial columns (e.g. lengths, thicknesses) directly into the SQL runtime.
3. **Auto-Healing**: Using semantic CSV attributes (e.g., `width=0.9`) to auto-correct imprecise geometric lines drawn by LLMs in SVG files during Sync-Out.

👉 **[See the full DuckDB & CLI Strategy for detailed mechanisms](duckdb-strategy.md)**.
