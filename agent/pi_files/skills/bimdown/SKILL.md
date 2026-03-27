---
name: bimdown
description: Create and modify building models in BimDown format. Most elements use CSV + SVG pairs; doors, windows, and spaces are CSV-only. Covers architecture, structure, and MEP disciplines.
---

# BimDown Format

BimDown is an AI-native building data format. Most elements use paired CSV (attributes) + SVG (2D geometry) files. Hosted elements (doors, windows) and spaces are CSV-only.

## Project Structure

```
project/
  global/
    level.csv          # Building levels (required)
    grid.csv           # Structural grids (optional)
  lv-1/                # One directory per level
    wall.csv + wall.svg
    door.csv              # CSV-only (position on host wall)
    window.csv            # CSV-only (position on host wall)
    column.csv + column.svg
    slab.csv + slab.svg
    space.csv             # CSV-only (seed point x,y + name)
    room_separator.csv + room_separator.svg  # virtual boundary lines
    ...more element types
  lv-2/
    ...
```

## Key Rules

1. **CSV and SVG are linked by `id`**: Elements with SVG have a unique id in both CSV row and SVG element `id` attribute.
2. **CSV and SVG use the same file name** (both singular): `wall.csv` pairs with `wall.svg`.
3. **Coordinates are in meters**, Y-axis points up. SVG uses `<g transform="scale(1,-1)">` to flip Y.
4. **IDs are unique within each level** and use prefix + number: e.g. `w-1`, `d-2`, `c-3`.
5. **Doors/windows are CSV-only** — no SVG file. Use `host_id` + `position` (0.0-1.0 along host wall, center of opening).
6. **Spaces are CSV-only** — seed point (x, y) inside the room, boundary auto-derived from walls + room_separators.
7. **Wall thickness is a CSV field** — SVG `stroke-width` is for rendering only.
8. **Defaults**: `base_offset` defaults to 0, `top_level_id` defaults to next level above.

## SVG Template

All SVG files follow this structure:
```xml
<?xml version="1.0" encoding="utf-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="x y w h">
  <g transform="scale(1,-1)">
    <!-- elements here -->
  </g>
</svg>
```
Set `viewBox` to a tight bounding box around all elements with ~1m padding.

## Global Tables

### level

- File: `global/level.csv`
- ID prefix: `lv-` (e.g. `lv-1`, `lv-2`)

**CSV columns:**
```
id,number,name,elevation
```

- id (required)
- number (required)
- elevation (required)

### grid

- File: `global/grid.csv`
- ID prefix: `gr-` (e.g. `gr-1`, `gr-2`)

**CSV columns:**
```
id,number,start_x,start_y,end_x,end_y
```

- id (required)
- number (required)
- start_x (required)
- start_y (required)
- end_x (required)
- end_y (required)

## Architecture

### wall

- CSV: `lv-{n}/wall.csv`
- SVG: `lv-{n}/wall.svg`
- ID prefix: `w-` (e.g. `w-1`, `w-2`)

**CSV columns:**
```
id,number,base_offset,top_level_id,top_offset,material,thickness
```

- id (required)
- top_level_id (ref → level)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- thickness (required)

**SVG geometry:** <line> with x1,y1,x2,y2 (wall centerline). stroke-width for rendering only, thickness is in CSV.

### door

- CSV: `lv-{n}/door.csv`
- ID prefix: `d-` (e.g. `d-1`, `d-2`)
- Hosted on: wall

**CSV columns:**
```
id,number,base_offset,host_id,position,material,width,height,operation,hinge_position,swing_side
```

- id (required)
- host_id (required, ref → element)
- position (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- width (required)
- operation (enum: single_swing | double_swing | sliding | folding | revolving)
- hinge_position (enum: start | end)
- swing_side (enum: left | right)

### window

- CSV: `lv-{n}/window.csv`
- ID prefix: `wn-` (e.g. `wn-1`, `wn-2`)
- Hosted on: wall

**CSV columns:**
```
id,number,base_offset,host_id,position,material,width,height
```

- id (required)
- host_id (required, ref → element)
- position (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- width (required)

### column

- CSV: `lv-{n}/column.csv`
- SVG: `lv-{n}/column.svg`
- ID prefix: `c-` (e.g. `c-1`, `c-2`)

**CSV columns:**
```
id,number,base_offset,top_level_id,top_offset,material,shape,size_x,size_y
```

- id (required)
- top_level_id (ref → level)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- shape (required, enum: rect | round)

**SVG geometry:** <circle> (round) or <rect> (rectangular) at column center.

### slab

- CSV: `lv-{n}/slab.csv`
- SVG: `lv-{n}/slab.svg`
- ID prefix: `sl-` (e.g. `sl-1`, `sl-2`)

**CSV columns:**
```
id,number,base_offset,material,function,thickness
```

- id (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- function (enum: floor | roof | finish)

**SVG geometry:** <polygon> with points attribute outlining the slab boundary.

### space

- CSV: `lv-{n}/space.csv`
- ID prefix: `sp-` (e.g. `sp-1`, `sp-2`)

**CSV columns:**
```
id,number,base_offset,x,y,name
```

- id (required)
- x (required)
- y (required)

### room_separator

- CSV: `lv-{n}/room_separator.csv`
- SVG: `lv-{n}/room_separator.svg`
- ID prefix: `rs-` (e.g. `rs-1`, `rs-2`)

**CSV columns:**
```
id,number,base_offset
```

- id (required)

**SVG geometry:** <line> with x1,y1,x2,y2 (virtual room boundary line, no thickness).

### stair

- CSV: `lv-{n}/stair.csv`
- SVG: `lv-{n}/stair.svg`
- ID prefix: `st-` (e.g. `st-1`, `st-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,top_level_id,top_offset,width,step_count
```

- id (required)
- start_z (required)
- end_z (required)
- top_level_id (ref → level)

**SVG geometry:** <polygon> outlining the stair footprint.

## Structure

### structure_wall

- CSV: `lv-{n}/structure_wall.csv`
- SVG: `lv-{n}/structure_wall.svg`
- ID prefix: `sw-` (e.g. `sw-1`, `sw-2`)

**CSV columns:**
```
id,number,base_offset,top_level_id,top_offset,material,thickness
```

- id (required)
- top_level_id (ref → level)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)

**SVG geometry:** <line> with x1,y1,x2,y2 (structural wall centerline).

### structure_column

- CSV: `lv-{n}/structure_column.csv`
- SVG: `lv-{n}/structure_column.svg`
- ID prefix: `sc-` (e.g. `sc-1`, `sc-2`)

**CSV columns:**
```
id,number,base_offset,top_level_id,top_offset,material,shape,size_x,size_y
```

- id (required)
- top_level_id (ref → level)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- shape (required, enum: rect | round | l_shape | t_shape | cross | i_shape | c_shape)

**SVG geometry:** <circle> or <rect> at column center.

### structure_slab

- CSV: `lv-{n}/structure_slab.csv`
- SVG: `lv-{n}/structure_slab.svg`
- ID prefix: `ss-` (e.g. `ss-1`, `ss-2`)

**CSV columns:**
```
id,number,base_offset,material,function,thickness
```

- id (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)
- function (enum: floor | roof)

**SVG geometry:** <polygon> outlining the structural slab.

### beam

- CSV: `lv-{n}/beam.csv`
- SVG: `lv-{n}/beam.svg`
- ID prefix: `bm-` (e.g. `bm-1`, `bm-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,shape,size_x,size_y,material
```

- id (required)
- start_z (required)
- end_z (required)
- shape (required, enum: rect | round | l_shape | t_shape | cross | i_shape | c_shape)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)

**SVG geometry:** <line> with x1,y1,x2,y2 (beam centerline).

### brace

- CSV: `lv-{n}/brace.csv`
- SVG: `lv-{n}/brace.svg`
- ID prefix: `br-` (e.g. `br-1`, `br-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,shape,size_x,size_y,material
```

- id (required)
- start_z (required)
- end_z (required)
- shape (required, enum: rect | round | l_shape | t_shape | cross | i_shape | c_shape)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)

**SVG geometry:** <line> with x1,y1,x2,y2 (brace centerline).

### isolated_foundation

- CSV: `lv-{n}/isolated_foundation.csv`
- SVG: `lv-{n}/isolated_foundation.svg`
- ID prefix: `if-` (e.g. `if-1`, `if-2`)

**CSV columns:**
```
id,number,base_offset,material,length,width,thickness
```

- id (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)

**SVG geometry:** <rect> or <circle> at foundation location.

### strip_foundation

- CSV: `lv-{n}/strip_foundation.csv`
- SVG: `lv-{n}/strip_foundation.svg`
- ID prefix: `sf-` (e.g. `sf-1`, `sf-2`)

**CSV columns:**
```
id,number,base_offset,material,width,thickness
```

- id (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)

**SVG geometry:** <line> with x1,y1,x2,y2 (strip foundation centerline).

### raft_foundation

- CSV: `lv-{n}/raft_foundation.csv`
- SVG: `lv-{n}/raft_foundation.svg`
- ID prefix: `rf-` (e.g. `rf-1`, `rf-2`)

**CSV columns:**
```
id,number,base_offset,material,thickness
```

- id (required)
- material (enum: concrete | steel | wood | clt | glass | aluminum | brick | stone | gypsum | insulation | copper | pvc | ceramic | fiber_cement | composite)

**SVG geometry:** <polygon> outlining the raft boundary.

## MEP

### duct

- CSV: `lv-{n}/duct.csv`
- SVG: `lv-{n}/duct.svg`
- ID prefix: `du-` (e.g. `du-1`, `du-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,shape,size_x,size_y,system_type,start_node_id,end_node_id
```

- id (required)
- start_z (required)
- end_z (required)
- shape (required, enum: rect | round)
- system_type (enum: hvac | plumbing | fire_protection | electrical | data_comm | gas | other)

**SVG geometry:** <line> with x1,y1,x2,y2 (duct centerline).

### pipe

- CSV: `lv-{n}/pipe.csv`
- SVG: `lv-{n}/pipe.svg`
- ID prefix: `pi-` (e.g. `pi-1`, `pi-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,shape,size_x,size_y,system_type,start_node_id,end_node_id
```

- id (required)
- start_z (required)
- end_z (required)
- shape (required, enum: rect | round)
- system_type (enum: hvac | plumbing | fire_protection | electrical | data_comm | gas | other)

**SVG geometry:** <line> with x1,y1,x2,y2 (pipe centerline).

### cable_tray

- CSV: `lv-{n}/cable_tray.csv`
- SVG: `lv-{n}/cable_tray.svg`
- ID prefix: `ct-` (e.g. `ct-1`, `ct-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,shape,size_x,size_y,system_type
```

- id (required)
- start_z (required)
- end_z (required)
- shape (required, enum: rect | round)
- system_type (enum: hvac | plumbing | fire_protection | electrical | data_comm | gas | other)

**SVG geometry:** <line> with x1,y1,x2,y2 (cable tray centerline).

### conduit

- CSV: `lv-{n}/conduit.csv`
- SVG: `lv-{n}/conduit.svg`
- ID prefix: `co-` (e.g. `co-1`, `co-2`)

**CSV columns:**
```
id,number,base_offset,start_z,end_z,shape,size_x,size_y,system_type
```

- id (required)
- start_z (required)
- end_z (required)
- shape (required, enum: rect | round)
- system_type (enum: hvac | plumbing | fire_protection | electrical | data_comm | gas | other)

**SVG geometry:** <line> with x1,y1,x2,y2 (conduit centerline).

### equipment

- CSV: `lv-{n}/equipment.csv`
- SVG: `lv-{n}/equipment.svg`
- ID prefix: `eq-` (e.g. `eq-1`, `eq-2`)

**CSV columns:**
```
id,number,base_offset,system_type,equipment_type
```

- id (required)
- system_type (enum: hvac | plumbing | fire_protection | electrical | data_comm | gas | other)
- equipment_type (enum: ahu | fcu | chiller | boiler | cooling_tower | fan | pump | transformer | panelboard | generator | water_heater | tank | other)

**SVG geometry:** <rect> at equipment location.

### terminal

- CSV: `lv-{n}/terminal.csv`
- SVG: `lv-{n}/terminal.svg`
- ID prefix: `tm-` (e.g. `tm-1`, `tm-2`)

**CSV columns:**
```
id,number,base_offset,system_type,terminal_type
```

- id (required)
- system_type (enum: hvac | plumbing | fire_protection | electrical | data_comm | gas | other)
- terminal_type (enum: supply_air_diffuser | return_air_grille | exhaust_air_grille | sprinkler_head | fire_alarm_device | light_fixture | power_outlet | data_outlet | plumbing_fixture | other)

**SVG geometry:** <circle> or <rect> at terminal location.

## Validation

After writing files, ALWAYS validate:
```bash
bimdown validate /project
```
Fix all reported issues before proceeding. Common errors:
- Missing SVG file for a CSV that requires one (walls, columns, slabs need SVG; doors, windows, spaces do not)
- Wrong column names (use `bimdown schema <table>` to check)
- Missing required fields (id is always required)
- Wrong ID prefix (e.g. `col-1` instead of `c-1`)
- Door/window position out of 0-1 range

## Available CLI Tools

```bash
bimdown validate /project          # Check all files for errors
bimdown info /project              # Print project summary (levels, element counts)
bimdown schema <table>             # Print column definitions for a table
bimdown query /project "<sql>"     # SQL query on project data (DuckDB)
```

## Example: Simple Room with Door

### global/level.csv
```csv
id,number,name,elevation
lv-1,,Ground Floor,0
lv-2,,First Floor,3.5
```

### lv-1/wall.csv
```csv
id,material,thickness
w-1,concrete,0.2
w-2,concrete,0.2
w-3,concrete,0.2
w-4,concrete,0.2
```

### lv-1/wall.svg
```xml
<?xml version="1.0" encoding="utf-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="-1 -9 12 10">
  <g transform="scale(1,-1)">
    <line id="w-1" x1="0" y1="0" x2="10" y2="0" stroke="black" stroke-width="0.2" stroke-linecap="square" />
    <line id="w-2" x1="10" y1="0" x2="10" y2="8" stroke="black" stroke-width="0.2" stroke-linecap="square" />
    <line id="w-3" x1="10" y1="8" x2="0" y2="8" stroke="black" stroke-width="0.2" stroke-linecap="square" />
    <line id="w-4" x1="0" y1="8" x2="0" y2="0" stroke="black" stroke-width="0.2" stroke-linecap="square" />
  </g>
</svg>
```

### lv-1/door.csv (no SVG needed)
```csv
id,host_id,position,material,width,height,operation
d-1,w-1,0.3,wood,0.9,2.1,single_swing
```

### lv-1/space.csv (no SVG needed)
```csv
id,x,y,name
sp-1,5,4,Living Room
```