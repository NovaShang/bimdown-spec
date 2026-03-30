# BimDown

An open-source, AI-native building data format.

BimDown uses **CSV** for attributes and **SVG** for 2D geometry — simple enough for any LLM to read and write, structured enough for real BIM workflows.

## Why

BIM data is trapped in proprietary formats that AI can't touch. BimDown fixes this:

- **CSV + SVG** — human-readable, git-diffable, AI-writable
- **SQL-queryable** — load into DuckDB, query with SQL
- **Revit interop** — import/export via the included Revit add-in
- **Extensible** — add new building analysis capabilities by writing a [skill](#ai-agent-skills)

## Quick Look

```
project/
  global/
    level.csv            # building levels
    grid.csv             # structural grids
  lv-1/
    wall.csv + wall.svg  # walls (CSV attributes + SVG geometry)
    door.csv             # doors (CSV-only, parametric position on wall)
    slab.csv + slab.svg  # floor slabs
    space.csv            # rooms (seed point + name)
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
<svg xmlns="http://www.w3.org/2000/svg" viewBox="-1 -9 12 10">
  <g transform="scale(1,-1)">
    <line id="w-1" x1="0" y1="0" x2="10" y2="0" stroke="black" stroke-width="0.2" />
    <line id="w-2" x1="10" y1="0" x2="10" y2="8" stroke="black" stroke-width="0.2" />
  </g>
</svg>
```

**door.csv** (no SVG needed — position is parametric)
```csv
id,host_id,position,width,height,operation
d-1,w-1,0.3,0.9,2.1,single_swing
```

## CLI

```bash
npm install -g bimdown

bimdown validate ./my-project     # check for errors
bimdown info ./my-project         # project summary
bimdown schema wall               # show column definitions
bimdown query ./my-project "SELECT id, material, thickness FROM wall"
```

## Revit Add-in

The `revit-addin/` directory contains a C# add-in for Autodesk Revit 2026+ that enables bidirectional sync between Revit models and BimDown format.

```bash
cd revit-addin
dotnet build BimDown.RevitAddin.csproj
```

## Format Spec

The full format specification lives in [`spec/csv-schema/`](./spec/csv-schema/). Key concepts:

- **All coordinates in meters**, Y-axis up
- **IDs are level-scoped** — unique within each `lv-N/` directory
- **Hosted elements** (doors, windows, openings) use `host_id` + `position` (0.0–1.0 along wall)
- **Spaces** are seed points — boundary auto-derived from walls
- **Materials** use a fixed enum for ESG/energy modeling: `concrete, steel, wood, clt, glass, aluminum, brick, stone, gypsum, insulation, copper, pvc, ceramic, fiber_cement, composite`
- **Mesh fallback** — any element can have an optional `mesh_file` (GLB) for precise 3D visualization

### Element Types

| Category | Elements |
|---|---|
| Architecture | wall, door, window, column, slab, roof, ceiling, space, opening, room_separator, stair, curtain_wall |
| Structure | structure_wall, structure_column, structure_slab, beam, brace, isolated/strip/raft_foundation |
| MEP | duct, pipe, cable_tray, conduit, equipment, terminal, mep_node |
| Other | level, grid, mesh (non-parametric elements) |

## AI Agent Integration

BimDown is designed to be operated by AI agents. The format's simplicity (CSV + SVG) means any LLM can read and write building data without specialized APIs.

To add domain capabilities (energy modeling, ESG reports, compliance checking), write a skill definition following the [OpenClaw SKILL.md format](https://github.com/nicepkg/openclaw). See [BimClaw](https://bimclaw.com) for a hosted AI agent that works with BimDown out of the box.

## Related Projects

- **[bimdown-editor](https://github.com/NovaShang/bimdown-editor)** — browser-based 2D/3D building editor
- **[BimClaw](https://bimclaw.com)** — SaaS platform with hosted AI agent, real-time collaboration, and domain-specific analysis tools

## License

MIT
