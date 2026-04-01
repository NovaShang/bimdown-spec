# BimDown Agent Skill & Schema Rules

You are an AI Agent operating within a BimDown project environment.
BimDown is an open-source, AI-native building data format using CSV and SVG.

## 🛠️ Global Directives

1. **CLI is your Toolkit**: ALWAYS use the `bimdown query` and `bimdown validate` commands to interact with data. Avoid writing raw parsers if CLI commands can fulfill the requirement.
2. **Synchronized Modification**: BimDown semantics live in CSV, geometry in SVG. If you add/modify/remove an entity in script, you MUST ensure both the CSV row and SVG node (if applicable) are handled synchronously.
3. **No Zombies**: Do not leave unhosted elements or missing references in the files. Use the schema definitions below to understand relationships.

## 📐 Schema Topologies & Constraints

The following rules are automatically derived from the core YAML schema specifications. 
Fields not listed here represent intuitive numeric/string standard attributes.

### Table: `beam` (Prefix: `bm`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `brace` (Prefix: `br`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `cable_tray` (Prefix: `ct`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")
  - `start_node_id`: ID of the upstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.
  - `end_node_id`: ID of the downstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.

### Table: `ceiling` (Prefix: `cl`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `height_offset`: Offset below level elevation (e.g. -0.3 for 30cm dropped ceiling)

### Table: `column` (Prefix: `c`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.

### Table: `conduit` (Prefix: `co`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")
  - `start_node_id`: ID of the upstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.
  - `end_node_id`: ID of the downstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.

### Table: `curtain_wall` (Prefix: `cw`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.
  - `u_grid_count`: Number of horizontal (U) grid lines
  - `v_grid_count`: Number of vertical (V) grid lines
  - `u_spacing`: Uniform U grid spacing in meters (null if irregular)
  - `v_spacing`: Uniform V grid spacing in meters (null if irregular)
  - `panel_count`: Total number of curtain panels
  - `panel_material`: Dominant panel material name

### Table: `door` (Prefix: `d`)
- **Topology Rule**: Must be hosted on a `wall`.
- **Rule**: Doors NEVER exist independently. When creating or modifying a door, you MUST ensure it is hosted on a valid wall segment. In scripts, ensure coordinates intersect the wall's line.
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `host_id`: Must reference a valid `element` ID.
  - `position`: Parametric position along host element (0.0 = start, 1.0 = end, center of opening)

### Table: `duct` (Prefix: `du`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")
  - `start_node_id`: ID of the upstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.
  - `end_node_id`: ID of the downstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.

### Table: `equipment` (Prefix: `eq`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")

### Table: `foundation` (Prefix: `f`)
- **Rule**: Unified foundation type. Geometry form is determined by the SVG element: - <rect>/<circle> (point-based): isolated/pad footing - <path> (line-based): strip/continuous footing - <polygon> (polygon-based): raft/mat foundation Computed geometry fields are hydrated from SVG at query time.

- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `width`: Width of strip foundation or pad dimension
  - `length`: Length of pad foundation

### Table: `mep_node` (Prefix: `mn`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")

### Table: `mesh` (Prefix: `ms`)
- **Field Constraints**:
  - `category`: Revit category name (e.g. "Railings", "Generic Models", "Furniture", "Plumbing Fixtures")
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Path to GLB file relative to project root
  - `rotation`: Rotation around Z axis in degrees

### Table: `opening` (Prefix: `op`)
- **Rule**: Opening (void) in a host element. host_id is always required. Geometry form depends on the host type: - Wall opening: host_id references a wall. Uses position (0-1 along wall),
  width, and height. No SVG geometry.
- Slab opening: host_id references a slab. Uses SVG geometry
  (<rect> or <polygon>) to define the void shape.

- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `host_id`: Must reference a valid `element` ID.
  - `position`: Parametric position along host wall (0.0-1.0). Only for wall openings.
  - `width`: Opening width. Required for wall openings.
  - `height`: Opening height. Only for wall openings.
  - `shape`: Opening shape. Only for wall openings.

### Table: `pipe` (Prefix: `pi`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")
  - `start_node_id`: ID of the upstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.
  - `end_node_id`: ID of the downstream node (equipment, terminal, or mep_node). Written by Revit export; supplemented by CLI resolve-topology.

### Table: `railing` (Prefix: `rl`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `ramp` (Prefix: `rp`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `roof` (Prefix: `ro`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `slope`: Slope angle in degrees (0 = flat roof)

### Table: `room_separator` (Prefix: `rs`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `slab` (Prefix: `sl`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `space` (Prefix: `sp`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `x`: Seed point X coordinate (room interior point)
  - `y`: Seed point Y coordinate (room interior point)

### Table: `stair` (Prefix: `st`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.

### Table: `structure_column` (Prefix: `sc`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.

### Table: `structure_slab` (Prefix: `ss`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization

### Table: `structure_wall` (Prefix: `sw`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.

### Table: `terminal` (Prefix: `tm`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `system_type`: Revit system classification (e.g. "CHWS", "CHR", "SA", "RA", "DW")

### Table: `wall` (Prefix: `w`)
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `top_level_id`: Must reference a valid `level` ID. Top constraint level. Empty = next level above current level.
  - `top_offset`: Offset from top level. Default 0.
  - `thickness`: Wall thickness in meters. SVG stroke-width should match but CSV is source of truth.

### Table: `window` (Prefix: `wn`)
- **Topology Rule**: Must be hosted on a `wall`.
- **Field Constraints**:
  - `level_id`: Must reference a valid `level` ID.
  - `mesh_file`: Optional GLB mesh path for precise 3D visualization
  - `host_id`: Must reference a valid `element` ID.
  - `position`: Parametric position along host element (0.0 = start, 1.0 = end, center of opening)

