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

## Base Mixins

| Mixin                     | Key Fields                                        |
|---------------------------|---------------------------------------------------|
| `element`                 | `id` (UUID, PK), `name`, `number`, `level_id`     |
| `line_element`            | `start_x/y`, `end_x/y`                            |
| `spatial_line_element`    | extends `line_element` + `start_z`, `end_z`       |
| `point_element`           | `x`, `y`, `rotation`                              |
| `polygon_element`         | `points` (serialized polygon)                     |
| `hosted_element`          | `host_id` (reference to host element), `location_param` |
| `vertical_span`           | `top_level_id`, `top_offset`                      |
| `materialized`            | `material`                                        |
| `section_profile`         | `shape` (rect/round), `size_x`, `size_y`          |
| `structural_section_profile` | `shape` (rect/round/l_shape/t_shape/…), `size_x`, `size_y` |
| `mep_system`              | `system_type` (enum)                              |
| `mep_connected_segment`   | `start_node_id`, `end_node_id` (connectivity)     |

---

## Design Decisions

- **Architecture vs Structure**: Architecture and structural elements are **decoupled**. Structural elements (`structure_column`, `structure_wall`, `structure_slab`) inherit directly from geometry base mixins, not from architecture classes. This prevents cross-discipline field conflicts.
- **`section_profile` vs `structural_section_profile`**: Architecture columns use the simpler `section_profile` (rect/round). Structural elements use `structural_section_profile` which includes engineering shapes (I, T, L…).
- **Stairs use `spatial_line_element`**: Stairs are 3D elements — they have a bottom landing point and a top landing point at different elevations, requiring `start_z`/`end_z`.
- **`id` is UUID**: All tables include a required `id` field (UUID string) as the primary key, enabling UUID-based linkage between CSVs and SVG layers.
- **`mep_system.system_type` is enum**: Controlled vocabulary prevents free-form strings and enables reliable filtering.
