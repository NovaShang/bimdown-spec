# BimDown Spec

This directory defines the **BimDown data format** — a "Minimum Viable Building" representation optimized for AI-native building design and bidirectional sync with BIM tools (Revit).

BimDown uses **CSV for attributes** and **SVG for 2D geometry**, both human-readable and AI-friendly. A CLI tool with DuckDB provides relational query and auto-sync capabilities.

---

## Project Structure

```text
{project-id}/
├── project_metadata.json        # Format version, project name, units, source
├── global/
│   ├── level.csv                # Floor level definitions
│   ├── grid.csv                 # Structural grid lines
│   ├── wall.csv / wall.svg      # Multi-story walls (span > 1 level above)
│   ├── stair.csv / stair.svg    # Multi-story stairs
│   └── ...                      # Any element spanning > 1 level above
├── 1F/
│   ├── wall.csv / wall.svg
│   ├── column.csv / column.svg
│   ├── slab.csv / slab.svg
│   ├── door.csv                 # No SVG (hosted, parametric position)
│   ├── window.csv               # No SVG (hosted, parametric position)
│   ├── space.csv                # No SVG (seed point only)
│   └── ...
├── 2F/
│   └── ...
└── _IdMap.csv                   # UUID ↔ short ID mapping (Revit round-trip)
```

---

## Partitioning Rules (Level vs Global)

All elements belong to a **base level** (their `level_id`).

- **Level directory**: Elements whose vertical extent stays within one level above their base (e.g. a 1F wall with `top_level_id` = 2F is normal single-story — goes in `1F/`).
- **Global directory**: Elements that span **more than one level above** their base (e.g. a 1F wall with `top_level_id` = 3F). Also: `level.csv`, `grid.csv`.
- **Elements without `vertical_span`**: Always go in their level directory.

---

## Schema Overview

### Global Tables

| Table   | SVG | Description |
|---------|-----|-------------|
| `level` | No  | Floor levels by elevation |
| `grid`  | No  | Structural/reference grid lines |

### Architecture

| Table            | Geometry        | SVG     | Description |
|------------------|-----------------|---------|-------------|
| `wall`           | Line            | `<path>` | Architectural wall with thickness and vertical span |
| `column`         | Point           | `<rect>`/`<circle>` | Architectural column with section profile |
| `slab`           | Polygon         | `<polygon>` | Floor/roof/finish slab |
| `space`          | Seed point      | `<polygon>` (computed) | Named space/room (boundary computed by `build` from walls, curtain walls, room separators) |
| `door`           | Hosted on wall  | No      | Door with operation type |
| `window`         | Hosted on wall  | No      | Window with dimensions |
| `opening`        | Hosted          | Conditional | Wall opening (no SVG) or slab opening (`<rect>`/`<polygon>`) |
| `stair`          | Spatial line    | `<path>` | Stair run with vertical span |
| `ramp`           | Spatial line    | `<path>` | Accessibility ramp |
| `railing`        | Spatial line    | `<path>` | Railing along path |
| `curtain_wall`   | Line            | `<path>` | Curtain wall with grid parameters |
| `ceiling`        | Polygon         | `<polygon>` | Ceiling surface |
| `roof`           | Polygon         | `<polygon>` | Roof surface |
| `room_separator` | Line            | `<path>` | Invisible boundary line for room separation |

### Structure

| Table              | Geometry   | SVG     | Description |
|--------------------|------------|---------|-------------|
| `structure_wall`   | Line       | `<path>` | Structural wall |
| `structure_column` | Point      | `<rect>`/`<circle>` | Structural column |
| `structure_slab`   | Polygon    | `<polygon>` | Structural slab |
| `beam`             | Spatial line | `<path>` | Structural beam |
| `brace`            | Spatial line | `<path>` | Structural brace |
| `foundation`       | Mixed      | `<rect>`/`<circle>`, `<path>`, or `<polygon>` | Unified foundation (geometry determines form) |

### MEP

| Table        | Geometry     | SVG     | Description |
|--------------|-------------|---------|-------------|
| `duct`       | Spatial line | `<path>` | HVAC duct (endpoints from connectors) |
| `pipe`       | Spatial line | `<path>` | Plumbing/process pipe (endpoints from connectors) |
| `cable_tray` | Spatial line | `<path>` | Electrical cable tray (endpoints from connectors) |
| `conduit`    | Spatial line | `<path>` | Electrical conduit (endpoints from connectors) |
| `equipment`  | Point       | `<rect>`/`<circle>` | MEP equipment (AHU, chiller, pump...) |
| `terminal`   | Point       | `<rect>`/`<circle>` | MEP terminal (diffuser, outlet...) |
| `mep_node`   | Point       | `<rect>`/`<circle>` | Topology node (fitting/accessory in Revit) |

### Fallback

| Table  | SVG | Description |
|--------|-----|-------------|
| `mesh` | No  | Generic 3D model (GLB) for elements without parametric schema |

---

## Schema Field Metadata

### `required: true`

Semantic **source of truth**. Written directly in `.csv` by AI. If SVG geometry contradicts a required field, the CLI uses the CSV value to auto-correct SVG on sync.

### `computed: true`

Spatial derivatives extracted from SVG at runtime. **Not stored in CSV**. The DuckDB CLI engine hydrates these as virtual columns during queries.

**Rule**: Elements with SVG have their geometry fields as `computed`. Elements without SVG (door, window, space, grid, level) have geometry fields as `required`.

---

## Base Mixins

| Mixin                        | Key Fields |
|------------------------------|------------|
| `element`                    | `id` (short ID, PK), `number`, `base_offset`, `mesh_file` |
| `line_element`               | `start_x/y`, `end_x/y`, `length` (all computed from SVG `<path>`) |
| `spatial_line_element`       | extends `line_element` + `start_z`, `end_z` (required, CSV) |
| `point_element`              | `x`, `y`, `rotation` (all computed from SVG) |
| `polygon_element`            | `points`, `area` (all computed from SVG) |
| `hosted_element`             | `host_id` (reference), `position` (distance in meters from host start) |
| `vertical_span`              | `top_level_id`, `top_offset`, `height` (computed) |
| `materialized`               | `material` (enum: concrete, steel, wood, clt, glass, aluminum, brick, stone, gypsum, insulation, copper, pvc, ceramic, fiber_cement, composite) |
| `section_profile`            | `shape` (rect/round), `size_x`, `size_y` |
| `structural_section_profile` | `shape` (rect/round/i/t/l/c/cross), `size_x`, `size_y` |
| `mep_system`                 | `system_type` (string, e.g. "CHWS", "SA", "DW") |
| `mep_connected_segment`      | `start_node_id`, `end_node_id` (auto-resolved by CLI) |

---

## ID System

All elements use prefixed short IDs: `{prefix}-{n}`. Counters are 1-based per table, scoped to the level directory.

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
| `opening` | `op` | `op-1` |
| `stair` | `st` | `st-1` |
| `ramp` | `rp` | `rp-1` |
| `railing` | `rl` | `rl-1` |
| `curtain_wall` | `cw` | `cw-1` |
| `ceiling` | `cl` | `cl-1` |
| `roof` | `ro` | `ro-1` |
| `room_separator` | `rs` | `rs-1` |
| `structure_wall` | `sw` | `sw-1` |
| `structure_column` | `sc` | `sc-1` |
| `structure_slab` | `ss` | `ss-1` |
| `beam` | `bm` | `bm-1` |
| `brace` | `br` | `br-1` |
| `foundation` | `f` | `f-1` |
| `duct` | `du` | `du-1` |
| `pipe` | `pi` | `pi-1` |
| `cable_tray` | `ct` | `ct-1` |
| `conduit` | `co` | `co-1` |
| `equipment` | `eq` | `eq-1` |
| `terminal` | `tm` | `tm-1` |
| `mep_node` | `mn` | `mn-1` |
| `mesh` | `ms` | `ms-1` |

Round-trip fidelity with Revit is maintained via a `BimDown_Id` shared parameter stored on each Revit element, and `_IdMap.csv` at the project root.

---

## Design Decisions

### Architecture vs Structure Decoupling

Architectural and structural elements are fully independent. `structure_column`, `structure_wall`, `structure_slab` inherit from geometry bases directly, not from architecture types. This prevents cross-discipline field conflicts and allows independent modeling.

### Section Profiles

- Architecture columns use `section_profile` (rect/round).
- Structural elements use `structural_section_profile` with engineering shapes (I, T, L, C, cross).

### SVG as AI-Native Geometry Storage

SVG is **not** used for visualization — it is a geometry storage format chosen because AI models have extensive training data on SVG and strong spatial reasoning with it. The spec uses standard SVG subset (`<path>`, `<rect>`, `<circle>`, `<polygon>`) without custom attributes or styling requirements.

Line elements use `<path>` with `M`, `L`, `A` commands. This naturally supports both straight lines and arcs (curved walls, ramps) using standard SVG syntax that AI models already understand well.

### Elements Without SVG

Some elements have no SVG representation:
- **Door/Window**: Fully defined by `host_id` + `position` (parametric placement on wall). Absolute coordinates would require re-syncing whenever the host wall moves.
- **Space**: Defined by seed point `(x, y)` in CSV. Boundary polygon is auto-computed by `bimdown build` from surrounding walls, curtain walls, room separators, and structure walls using a half-edge face tracing algorithm. The generated `space.svg` contains `<polygon>` elements whose IDs match the space CSV rows.
- **Grid/Level**: Global reference data with coordinates in CSV.

### Unified Foundation Type

Rather than separate types for isolated/strip/raft foundations, a single `foundation` table covers all forms. The geometry type (point/line/polygon) is determined by the SVG element. This reduces table count while the SVG naturally disambiguates the form.

### Opening: Wall and Slab Voids

`opening` supports two modes via the same table:
- **Wall opening**: `host_id` → wall, with `position`, `width`, `height`. No SVG.
- **Slab opening**: `host_id` → slab, with SVG geometry (`<rect>` or `<polygon>`).

For multi-story shaft openings: export one `opening` per level, each hosted on its respective slab.

### MEP Topology

MEP networks form a **bipartite graph**: `mep_curve` (duct, pipe, cable_tray, conduit) connects to `mep_node` (fittings, accessories), and nodes connect back to curves. Two curves never connect directly — there is always a node in between.

- **mep_curve** geometry is defined by its two connector endpoints (not the physical centerline). In SVG this is a `<path>`. In Revit, endpoints are taken from `Connector.Origin` positions, which naturally align with the connectors of adjacent fittings.
- **mep_node** is a minimal topology node with position only. In SVG this is a `<rect>`. In Revit it maps to fittings (`DuctFitting`, `PipeFitting`, etc.) and accessories (`DuctAccessory`, `PipeAccessory`).
- **equipment** and **terminal** also serve as network endpoints — curves can connect directly to them.

**AI authoring workflow**:
1. Place equipment and terminals (anchors)
2. Draw duct/pipe segments connecting them (endpoint coordinates)
3. Call CLI `build` / `resolve-topology` — this detects coincident endpoints, generates `mep_node` entries at junctions, and back-fills `start_node_id`/`end_node_id` on each segment. Warns about disconnected endpoints.

**Revit export**: Fittings and accessories are exported as `mep_node`. Curve endpoints are taken from connector positions (not `LocationCurve`), so they naturally coincide with node positions. `start_node_id`/`end_node_id` are derived from Revit's connector relationships.

**Revit import**: Curves are created from endpoint coordinates. Fittings are auto-inserted at junctions where curves meet nodes.

### Material Enum

A fixed enum of 15 common structural/architectural materials. Represents the **primary material** of an element. Composite/multi-layer constructions are not modeled — the Revit plugin handles layer composition independently.

### Format Versioning

`project_metadata.json` at the project root includes `format_version` for forward compatibility.

---

## Storage & Query Architecture

See **[DuckDB & CLI Strategy](duckdb-strategy.md)** for details on:
1. **Hydration**: Merging partitioned CSVs + parsing SVG geometry into in-memory DuckDB tables.
2. **Execution**: Standard SQL queries over unified, geometry-enriched views.
3. **Sync-Out**: Stripping computed fields, auto-healing SVG from CSV source-of-truth, re-partitioning by level.
4. **Resolve-Topology**: Auto-generating MEP connectivity graph from endpoint coordinates.
