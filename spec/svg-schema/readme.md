# BimDown SVG Spec

SVG is the **geometry storage layer** for BimDown. It is not used for visualization — it is chosen because AI models have extensive training data on SVG and strong spatial reasoning with the format.

---

## 1. File Organization

SVG files are co-located with their CSV counterparts, organized by level:

```text
{project-id}/
  {level}/
    wall.svg
    column.svg
    slab.svg
    ...
  global/
    wall.svg       (multi-story walls)
    ...
```

Each SVG file corresponds to one element table for one level. No `global/` SVG folder exists for cross-floor geometry — multi-story elements in `global/*.csv` are projected onto their base level's SVG by the CLI.

### Elements Without SVG

The following element types have **no SVG files**:
- `door`, `window` — Parametric placement via `host_id` + `position` on wall
- `space` — Seed point `(x, y)` in CSV; boundary auto-derived from walls
- `opening` (wall mode) — Parametric placement via `host_id` + `position`
- `level`, `grid` — Global reference data with coordinates in CSV
- `mesh` — 3D geometry in GLB files

---

## 2. Coordinate System

- **Origin**: Project Cartesian origin `(0, 0)`
- **Units**: Meters
- **Y-Axis**: Architectural convention (+Y = North). When rendering, apply `<g transform="scale(1, -1)">` to flip to SVG screen coordinates.

---

## 3. SVG Subset

### Allowed Elements

| SVG Element | BimDown Usage |
|-------------|---------------|
| `<svg>`, `<g>` | Structure and grouping |
| `<path>` | Line elements (walls, beams, ducts, stairs, ramps, railings, etc.) |
| `<rect>` | Point elements with rectangular profile |
| `<circle>` | Point elements with round profile |
| `<polygon>` | Polygon elements (slabs, ceilings, roofs, etc.) |
| `<text>` | Optional labels |

### Allowed `<path>` Commands

Only the following path commands are permitted:
- `M` / `m` — Move to (start point)
- `L` / `l` — Line to (straight segment)
- `A` / `a` — Arc to (circular arc segment)

**Bezier curves (`C`, `Q`, `S`, `T`) are forbidden.** Arcs cover the curved geometry needed in architecture (curved walls, ramps).

### Forbidden Features

- `<defs>`, `<use>`, gradients, filters, animations
- Embedded scripts (`<script>`)
- Bezier path commands

### Styling

The spec does **not** require or read any styling attributes (`stroke`, `stroke-width`, `fill`, etc.). AI may write them freely for valid SVG, but they are ignored on import. Only geometric attributes and `id` are meaningful.

---

## 4. Element Representation

Every SVG element **must** have an `id` attribute matching the CSV short ID (e.g. `w-1`, `c-3`).

### 4.1 Line Elements → `<path>`

Walls, beams, braces, ducts, pipes, stairs, ramps, railings, curtain walls, room separators, strip foundations.

```xml
<!-- Straight wall from (0,0) to (5,0) -->
<path id="w-1" d="M 0,0 L 5,0" />

<!-- Curved wall: arc from (0,0) to (5,0) -->
<path id="w-2" d="M 0,0 A 3,3 0 0,1 5,0" />
```

- Straight segments: `M x1,y1 L x2,y2`
- Arcs: `M x1,y1 A rx,ry rotation large-arc-flag sweep-flag x2,y2`
- One `<path>` per element (one-to-one mapping with CSV rows)

### 4.2 Point Elements → `<rect>` or `<circle>`

Columns, structure columns, equipment, terminals, mep_nodes, isolated foundations.

```xml
<!-- Rectangular column 0.4×0.4 at (2,2) -->
<rect id="c-1" x="1.8" y="1.8" width="0.4" height="0.4" />

<!-- Round column at (5,3) with radius 0.2 -->
<circle id="c-2" cx="5" cy="3" r="0.2" />

<!-- Rotated rectangular column -->
<rect id="c-3" x="1.8" y="1.8" width="0.4" height="0.6" transform="rotate(45, 2, 2.1)" />
```

- `shape = "round"` → `<circle>`, otherwise → `<rect>`
- Rotation via `transform="rotate(angle, center_x, center_y)"`

### 4.3 Polygon Elements → `<polygon>`

Slabs, structure slabs, ceilings, roofs, raft foundations, slab openings.

```xml
<!-- Floor slab -->
<polygon id="sl-1" points="0,0 10,0 10,8 0,8" />
```

- `points` attribute: space-separated `x,y` coordinate pairs

### 4.4 Foundation (Mixed Geometry)

A single `foundation` table uses different SVG elements depending on the form:
- Isolated (pad): `<rect>` or `<circle>` (point-based)
- Strip (continuous): `<path>` (line-based)
- Raft (mat): `<polygon>` (polygon-based)

```xml
<!-- Isolated foundation -->
<rect id="f-1" x="0.4" y="0.4" width="1.2" height="1.2" />
<!-- Strip foundation -->
<path id="f-2" d="M 0,0 L 10,0" />
<!-- Raft foundation -->
<polygon id="f-3" points="0,0 10,0 10,8 0,8" />
```

### 4.5 Slab Opening

When `opening.host_id` references a slab, the opening has SVG geometry:

```xml
<!-- Rectangular slab opening -->
<rect id="op-1" x="3" y="3" width="2" height="1.5" />
<!-- Irregular slab opening -->
<polygon id="op-2" points="3,3 5,3 5,4.5 3,4.5" />
```

---

## 5. Computed Field Hydration

The CLI parses SVG elements and injects computed fields into DuckDB:

| SVG Element | Computed Fields |
|-------------|----------------|
| `<path>` (line) | `start_x`, `start_y`, `end_x`, `end_y`, `length` |
| `<rect>` | `x`, `y`, `size_x` (width), `size_y` (height), `rotation` |
| `<circle>` | `x` (cx), `y` (cy), `size_x` (2r), `size_y` (2r), `shape="round"` |
| `<polygon>` | `points` (serialized), `area` |
