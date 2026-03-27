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
- Plan door/window positions as parametric values (0-1) along host walls
- All units in meters

### Step 3: Look Up Schemas

Run `bimdown_schema` for each table you plan to use. **Do not guess column names.** Common mistakes:
- Forgetting `position` for doors/windows (required, 0.0-1.0)
- Forgetting `thickness` in wall.csv (it's now a required CSV field)
- Wrong enum values for `operation`, `shape`, `function`

### Step 4: Create Files — Build After Each Step

**Order matters.** References must point to existing IDs. **Run `bimdown_build` after each file or pair** to catch errors early.

1. **`global/level.csv`** — Define all levels with elevations → `bimdown_build`
2. **`global/grid.csv`** — Define structural grids (can be header-only if not needed) → `bimdown_build`
3. **For each level directory (`lv-N/`):**
   1. `wall.csv` + `wall.svg` → `bimdown_build`
   2. `slab.csv` + `slab.svg` → `bimdown_build`
   3. `column.csv` + `column.svg` (if needed) → `bimdown_build`
   4. `door.csv` (CSV only, no SVG) → `bimdown_build`
   5. `window.csv` (CSV only, no SVG) → `bimdown_build`
   6. `space.csv` (CSV only, no SVG) → `bimdown_build`
   7. `room_separator.csv` + `room_separator.svg` (if needed) → `bimdown_build`

If a build fails, fix the errors before writing the next file.

### Step 5: Render, Evaluate, and Iterate

Run `bimdown_render` for each level to see the floor plan as an image. This is for your internal verification only — never share the rendered image with the user.

**Compare the render against the user's requirements.** For each level, evaluate:
- Do the room sizes and proportions match what was requested?
- Are all requested rooms present and in sensible locations?
- Do walls form a closed envelope (no gaps)?
- Are doors and windows positioned sensibly on their host walls?
- Is the overall layout practical?

**Iterate until the layout is correct.** Expect 2-3 iterations minimum.

### Step 6: Verify with Info

Run `bimdown_info` to confirm the model looks correct — right number of levels, elements per floor, etc.

## Common Pitfalls (ordered by frequency)

### 1. Wrong units — ALL values in METERS, not millimeters
This applies to BOTH SVG coordinates AND CSV fields. Common mistakes:
- `elevation=3500` → WRONG, should be `3.5` (3.5 meters)
- `width=900` → WRONG, should be `0.9` (0.9 meters)
- `x2="10000"` → WRONG, should be `x2="10"`

**Quick check**: if any number is > 100, you are almost certainly using millimeters. Divide by 1000.

### 2. Door/window position out of range
`position` must be between 0.0 and 1.0. It represents the center of the opening along the host wall:
- `position=0.0` = start of wall
- `position=0.5` = middle of wall
- `position=1.0` = end of wall

### 3. IDs are level-scoped
IDs must be unique within each `lv-N/` directory. You can reuse `w-1` on different levels. But within the same level, every ID must be unique across all tables.

### 4. Missing viewBox in SVG
Every SVG must have a `viewBox`. Compute from geometry + 1m padding. With `scale(1,-1)`:
- Geometry x: 0 to 20, y: 0 to 15 → `viewBox="-1 -16 22 17"`

### 5. Missing `scale(1,-1)` in SVG
Every SVG file **must** have `<g transform="scale(1,-1)">`. Without it validation fails.

### 6. Mismatched IDs between CSV and SVG
For elements with SVG, every `id` in CSV must appear as `id` attribute on exactly one SVG element.

### 7. Forgetting grid.csv
`global/grid.csv` is required even if empty. At minimum:
```csv
id,number,start_x,start_y,end_x,end_y
```

### 8. Polygon point order
Slab polygon points must form a valid closed shape (clockwise or counter-clockwise).

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
