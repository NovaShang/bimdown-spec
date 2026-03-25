---
name: create-building-model
description: Step-by-step workflow for creating a complete BimDown building model from scratch. Covers the correct file creation order, common pitfalls, and validation checklist.
---

# Creating a BimDown Building Model

This guide covers the correct process for creating a building model from scratch in BimDown format.

## Step-by-Step Workflow

### Step 1: Understand the Requirements

Before writing any files, clarify:
- How many floors/levels?
- What rooms on each floor? (names, rough sizes)
- Any structural requirements? (columns, special slabs)
- Door and window placement?

If the user gives vague instructions like "a 3-bedroom apartment", use reasonable architectural defaults.

### Step 2: Plan the Geometry

Sketch out coordinates mentally before writing files:
- Pick an origin (0,0) — typically the bottom-left corner of the building footprint
- Determine overall building dimensions
- Plan wall positions as line segments (start/end points)
- Plan room polygons inside the walls
- All units in meters

### Step 3: Look Up Schemas

Run `bimdown_schema` for each table you plan to use. **Do not guess column names.** Common mistakes:
- Using `thickness` in wall.csv (it's a computed field from SVG `stroke-width`, not a CSV column)
- Forgetting `host_id` for doors/windows
- Wrong enum values for `operation`, `shape`, `function`

### Step 4: Create Files in This Order

**Order matters.** References must point to existing IDs.

1. **`global/level.csv`** — Define all levels with elevations
2. **`global/grid.csv`** — Define structural grids (can be header-only if not needed)
3. **For each level directory (`lv-N/`):**
   1. `wall.csv` + `wall.svg` — Walls first (doors/windows reference them)
   2. `slab.csv` + `slab.svg` — Floor slab
   3. `column.csv` + `column.svg` — If needed
   4. `door.csv` + `door.svg` — After walls (needs `host_id` referencing wall IDs)
   5. `window.csv` + `window.svg` — After walls (needs `host_id`)
   6. `space.csv` + `space.svg` — Room definitions

### Step 5: Validate

Run `bimdown_validate` and fix every error. Do not skip this step.

### Step 6: Render, Evaluate, and Iterate

Run `bimdown_render` for each level to see the floor plan as an image. This is for your internal verification only — never share the rendered image with the user.

**Compare the render against the user's requirements.** For each level, evaluate:
- Do the room sizes and proportions match what was requested?
- Are all requested rooms present and in sensible locations?
- Do walls form a closed envelope (no gaps)?
- Do doors and windows sit on their host walls (not floating)?
- Are spaces filling the expected rooms without overlap?
- Is the overall layout practical? (e.g. hallway connects rooms, bathroom not inside kitchen)

**Iterate until the layout is correct.** This is a loop, not a single pass:
1. Render → evaluate against requirements → identify issues
2. Fix geometry (adjust coordinates, reposition walls/doors/spaces)
3. Re-validate with `bimdown_validate`
4. Render again → re-evaluate
5. Repeat until the floor plan matches the user's intent

Do not stop after the first render. Expect 2-3 iterations minimum. Only proceed to the next step when you are confident the layout is correct.

### Step 7: Verify with Info

Run `bimdown_info` to confirm the model looks correct — right number of levels, elements per floor, etc.

## Common Pitfalls (ordered by frequency)

### 1. Wrong units — ALL values in METERS, not millimeters
This applies to BOTH SVG coordinates AND CSV fields. Common mistakes:
- `elevation=3500` → WRONG, should be `3.5` (3.5 meters)
- `top_offset=3000` → WRONG, should be `3.0`
- `width=900` → WRONG, should be `0.9` (0.9 meters)
- `x2="10000"` → WRONG, should be `x2="10"`
- `stroke-width="200"` → WRONG, should be `stroke-width="0.2"`

**Quick check**: if any number is > 100, you are almost certainly using millimeters. Divide by 1000.

### 2. Duplicate IDs across levels
IDs are **globally unique**. If lv-1 uses `w-1` to `w-10`, then lv-2 MUST start at `w-11`. Same for all types: `d-1`..`d-5` on lv-1 means lv-2 starts at `d-6`. Never reuse IDs.

### 3. Missing viewBox in SVG
Every SVG must have a `viewBox`. Compute from geometry + 1m padding. With `scale(1,-1)`:
- Geometry x: 0 to 20, y: 0 to 15 → `viewBox="-1 -16 22 17"`

### 4. Missing `scale(1,-1)` in SVG
Every SVG file **must** have `<g transform="scale(1,-1)">`. Without it validation fails.

### 5. Mismatched IDs between CSV and SVG
Every `id` in CSV must appear as `id` attribute on exactly one SVG element. Missing or extra IDs = validation error.

### 6. Doors/Windows missing `data-host`
Hosted elements must have `data-host="w-N"` on their SVG element, matching `host_id` in CSV.

### 7. Door/Window geometry outside host wall
The `<line>` for a door/window must lie along its host wall's centerline.

### 8. Forgetting grid.csv
`global/grid.csv` is required even if empty. At minimum:
```csv
id,number,start_x,start_y,end_x,end_y
```

### 9. Polygon point order
Slab/space polygon points must form a valid closed shape (clockwise or counter-clockwise).

## Typical Dimensions (Reference)

| Element | Typical Dimension |
|---------|-------------------|
| Floor-to-floor height | 3.0 - 3.5m |
| External wall thickness | 0.2 - 0.3m |
| Internal wall thickness | 0.1 - 0.15m |
| Standard door width | 0.8 - 0.9m |
| Double door width | 1.5 - 1.8m |
| Standard door height | 2.1m |
| Window width | 1.0 - 1.8m |
| Window height | 1.2 - 1.5m |
| Window sill height | 0.9m |
| Bedroom | 3.0 x 3.5m to 4.0 x 5.0m |
| Living room | 4.0 x 5.0m to 6.0 x 8.0m |
| Kitchen | 2.5 x 3.0m to 4.0 x 4.0m |
| Bathroom | 1.8 x 2.5m to 3.0 x 3.5m |
| Hallway width | 1.2 - 1.5m |
| Column size (residential) | 0.3 x 0.3m to 0.5 x 0.5m |
| Slab thickness | 0.15 - 0.25m |
