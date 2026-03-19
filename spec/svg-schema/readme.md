# BIMDown SVG Spec

This directory defines the **SVG schema** for BIMDown — the geometry representation layer ("The Eyes").

Instead of a monolithic 3D or 2D file, BIMDown stores spatial data in standard, strictly-subset SVG files. This allows AI models to natively "see" geometry and parse coordinate data using text.

---

## 1. File Structure & Organization

SVGs are organized hierarchically by level (floor) and element category. This structural separation prevents files from becoming too large and makes selective loading easy.

```text
📁 {project-id}/
  📁 {level_name_or_id}/       (e.g., 1F, Level_1)
    📄 walls.svg          (All line-based walls for this level)
    📄 columns.svg        (Point-based structure)
    📄 slabs.svg          (Polygon floors/slabs)
    📄 spaces.svg         (Room polygons)
    📄 doors.svg          (Hosted elements)
```

By decoupling these into separate SVG files, an AI agent or renderer can easily overlay geometries like toggling layers: `walls.svg` + `columns.svg` + `doors.svg`.

---

## 2. Coordinate System & Units

BIMDown aligns its coordinate system directly with standard architectural authoring tools (like Revit), ensuring seamless bidirectional sync:

*   **Origin**: Uses the exact project Cartesian origin `(0,0)` defined in the source BIM/CSV.
*   **Units**: **Meters (m)**. (A coordinate length of `5.5` means 5.5 meters).
*   **Y-Axis Transformation**: 
    *   Architectural/Revit convention: +Y is **Up** (North).
    *   Standard SVG convention: +Y is **Down**.
    *   **Rule**: We retain the true architectural coordinates in the raw numerical data. When rendering or wrapping these files, a global transform **must** be applied at the parent `<svg>` or `<g>` group to flip the Y-axis:
      `<g transform="scale(1, -1)">`

---

## 3. Strict SVG Feature Subset

To remain "AI-Friendly" and lightweight, BIMDown strictly limits allowed SVG features. Complex styling, gradients, animations, or redundant wrapper tags are forbidden.

### Allowed Tags
*   **Structure**: `<svg>`, `<g>`
*   **Geometry**: `<line>`, `<rect>`, `<polygon>`, `<circle>`
*   *(Optional)* `<text>` for labels.

### Forbidden Features
*   `<path>` containing complex bezier curves (unless absolutely necessary for freeform organic shapes). We prefer discretized `<polygon>` arrays.
*   `<defs>`, `<use>`, gradients, filters, animations.
*   Embedded scripts (`<script>`).

---

## 4. Element Representation Rules

Geometries are mapped based on their placement paradigm in the CSV schema (`line_element`, `point_element`, `polygon_element`). 

Every drawn element **must** include its corresponding Attribute Layer UUID as the `id` property.

### 4.1 Line-Based Elements (e.g., Wall, Beam)
Instead of drawing the boundary of a wall as a complex polygon, we draw its **centerline** and represent its physical thickness via `stroke-width`. This makes inferencing and modifying topology trivial for Large Language Models.

*   **SVG Tag**: `<line>`
*   **Mapping**: 
    *   `(x1, y1)` to `(x2, y2)` matches the CSV axis.
    *   `stroke-width` = `thickness` (in meters).
    *   `stroke-linecap="square"` is recommended to maintain wall-end volume.
*   **Example (`walls.svg`)**:
    ```xml
    <!-- A wall that is 0.2m thick connecting (0,0) and (5,0) -->
    <line id="uuid-wall-1234" x1="0" y1="0" x2="5" y2="0" stroke="black" stroke-width="0.2" stroke-linecap="square" />
    ```

### 4.2 Point-Based Elements (e.g., Column, Equipment)
Elements placed at a single point `(x, y)` with a defined profile (e.g., rectangular).

*   **SVG Tag**: `<rect>` (or `<circle>` for round profiles).
*   **Mapping**:
    *   Positioned directly by the insertion point `(x, y)` from CSV.
    *   Dimensions are mapped to `width` and `height`.
    *   Rotation is applied via `transform="rotate(angle, center_x, center_y)"`.
*   **Example (`columns.svg`)**:
    ```xml
    <!-- A 0.4m x 0.4m column at (2, 2). Rendered centered -->
    <rect id="uuid-col-5678" x="1.8" y="1.8" width="0.4" height="0.4" fill="black" />
    ```

### 4.3 Polygon-Based Elements (e.g., Space, Slab)
Elements defined by a closed loop of points.

*   **SVG Tag**: `<polygon>`
*   **Mapping**:
    *   `points` attribute contains the space-separated list of `x,y` coordinates.
*   **Example (`spaces.svg`)**:
    ```xml
    <!-- A simple 5x5m room -->
    <polygon id="uuid-sp-9012" points="0,0 5,0 5,5 0,5" fill="rgba(0,0,255,0.1)" stroke="blue" stroke-width="0.05" />
    ```
