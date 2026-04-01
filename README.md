# BimDown

[![NPM Version](https://img.shields.io/npm/v/bimdown.svg)](https://www.npmjs.com/package/bimdown)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![CI Status](https://github.com/NovaShang/BimDown/actions/workflows/ci.yml/badge.svg)](https://github.com/NovaShang/BimDown/actions)

An open-source, AI-native building data format.

BimDown uses **CSV** for attributes and **SVG** for 2D geometry — simple enough for any LLM to read and write, structured enough for real BIM workflows.

## Positioning

BimDown is a **LOD 200 lightweight alternative to Revit**, targeting the schematic design phase. It captures building elements at the level of "what, where, and how big" — not "how exactly it's constructed."

Use BimDown when you need:
- AI agents to read, write, and reason about building data directly
- Git-based version control and diffing for building models
- SQL queries over building data (via DuckDB)
- A lightweight interchange format between design tools

Use Revit (or other full BIM tools) when you need:
- Construction-level detail (LOD 300+)
- Multi-layer wall/slab assemblies
- Structural/energy analysis with physical material properties
- Construction documentation and detailing

## Why

BIM data is trapped in proprietary formats that AI can't touch. BimDown fixes this:

- **CSV + SVG** — human-readable, git-diffable, AI-writable
- **SQL-queryable** — load into DuckDB, query with SQL
- **Revit interop** — bidirectional import/export via the included Revit add-in
- **Extensible** — add new building analysis capabilities by writing a [skill](#ai-agent-integration)

## Quick Look

```
project/
  project_metadata.json    # format version, project name, units
  global/
    level.csv              # building levels
    grid.csv               # structural grids
  lv-1/
    wall.csv + wall.svg    # walls (CSV attributes + SVG geometry)
    door.csv               # doors (CSV-only, parametric position on wall)
    slab.csv + slab.svg    # floor slabs
    space.csv              # rooms (seed point + name)
    ...
```

**wall.csv**
```csv
id,material,thickness
w-1,concrete,0.2
w-2,concrete,0.2
```

**wall.svg**
```xml
<svg xmlns="http://www.w3.org/2000/svg">
  <g transform="scale(1,-1)">
    <path id="w-1" d="M 0,0 L 10,0" />
    <path id="w-2" d="M 10,0 L 10,8" />
  </g>
</svg>
```

**door.csv** (no SVG needed — position is parametric)
```csv
id,host_id,position,width,height,operation
d-1,w-1,0.3,0.9,2.1,single_swing
```

## What BimDown Cannot Represent

The following Revit scenarios fall outside BimDown's scope and will be exported as `mesh` (GLB fallback) or lost:

### Geometry Limitations
- **Free-form / NURBS geometry** — conceptual mass, adaptive components, in-place families with complex shapes
- **Non-circular curved walls** — elliptical arcs, spline walls (circular arcs are supported)
- **Sloped slabs** — sub-element shape editing, slope arrows, variable-thickness slabs
- **Edited wall profiles** — non-rectangular wall sections (e.g. gable walls with sloped tops)
- **Per-panel curtain wall detail** — individual panel materials, embedded doors, non-rectangular panels

### Data Limitations
- **Multi-layer assemblies** — wall/slab/roof layer composition (core, finish, insulation layers)
- **Family types and parameters** — Revit type system, instance parameters, formulas, constraints
- **Phases** — existing/demolish/new construction phasing
- **Design options** — alternative design scenarios in the same model
- **Groups and arrays** — repeated element patterns
- **Nested families** — families containing other families
- **Worksets and linked models** — multi-user collaboration, cross-model references

### Discipline Limitations
- **Structural analysis** — loads, boundary conditions, rebar detailing
- **MEP calculations** — flow rates, pressure drops, electrical loads, heating/cooling loads
- **Energy modeling** — thermal properties (U-values, SHGC), occupancy schedules (geometry is sufficient but thermal attributes are missing)
- **Site and topography** — toposolids, property lines, site components
- **Furniture and fixtures** — exported as `mesh` (GLB) rather than parametric elements
- **Views and sheets** — sections, elevations, detail views, annotation, dimensions, schedules

## CLI

```bash
npm install -g bimdown

bimdown validate ./my-project     # check for errors
bimdown info ./my-project         # project summary
bimdown schema wall               # show column definitions
bimdown query ./my-project "SELECT id, material, thickness FROM wall"
```

## Revit Add-in

The `revit-addin/` directory contains a C# add-in for Autodesk Revit 2025+ that enables bidirectional sync between Revit models and BimDown format.

**Installation (Easiest)**:
Download the latest `BimDownInstaller.exe` from the [GitHub Releases](https://github.com/NovaShang/BimDown/releases) page and run it.

**Manual Build (Windows)**:
```powershell
cd revit-addin
.\publish.ps1
```

## Format Spec

The full format specification lives in [`spec/`](./spec/). Key concepts:

- **All coordinates in meters**, Y-axis = North
- **IDs are level-scoped** — unique within each `lv-N/` directory
- **Hosted elements** (doors, windows, openings) use `host_id` + `position` (0.0-1.0 along wall)
- **Spaces** are seed points — boundary auto-derived from surrounding walls
- **Materials** use a fixed enum: `concrete, steel, wood, clt, glass, aluminum, brick, stone, gypsum, insulation, copper, pvc, ceramic, fiber_cement, composite`
- **SVG geometry** uses `<path>` (M, L, A commands), `<rect>`, `<circle>`, `<polygon>` — no Bezier curves
- **Mesh fallback** — any element can have an optional `mesh_file` (GLB) for 3D visualization
- **MEP topology** — bipartite graph of curves and nodes, auto-resolved by CLI

### Element Types

| Category | Elements |
|---|---|
| Architecture | wall, column, slab, door, window, opening, space, stair, ramp, railing, curtain_wall, ceiling, roof, room_separator |
| Structure | structure_wall, structure_column, structure_slab, beam, brace, foundation |
| MEP | duct, pipe, cable_tray, conduit, equipment, terminal, mep_node |
| Other | level, grid, mesh (non-parametric fallback) |

## AI Agent Integration

BimDown is designed to be operated by AI agents. The format's simplicity (CSV + SVG) means any LLM can read and write building data without specialized APIs.

To add domain capabilities (energy modeling, ESG reports, compliance checking), write a skill definition following the [OpenClaw SKILL.md format](https://github.com/nicepkg/openclaw). See [BimClaw](https://bimclaw.com) for a hosted AI agent that works with BimDown out of the box.

## Related Projects

- **[bimdown-editor](https://github.com/nicepkg/bimdown-editor)** — browser-based 2D/3D building editor
- **[BimClaw](https://bimclaw.com)** — SaaS platform with hosted AI agent, real-time collaboration, and domain-specific analysis tools

## License

MIT
