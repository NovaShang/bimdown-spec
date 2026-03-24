# BIMDown: DuckDB & CLI Integration Strategy

The core challenge of **Agentic AI** in native CAD/BIM environments is a paradoxical requirement:
1. **Generative Tasks** need tiny, highly bounded contexts (like a single floor's wall setup) to avoid LLM hallucination and context-window exhaustion.
2. **Analytical Tasks & Editing** require massive, cross-disciplinary, entire-building relational views, along with complex geometry calculations (which LLMs are mathematically terrible at).

BIMDown bridges this via an intelligent **Python CLI Tool** wrapped around **DuckDB**. 

The CLI acts as the middleware syncing the "Eyes" (SVG) and the "Brain" (CSV), providing the LLM with a flawless, unified Database without compromising the lightweight physical file structure.

---

## 1. The "Hydration" Phase (Import)

When the Agent requires global context (e.g., executing a SQL command), the CLI tool initializes an in-memory DuckDB database and dynamically "hydrates" the tables.

### A. Union of Partitions
The CLI merges physical, fragmented level-specific CSV files into single logical tables.
```sql
CREATE TABLE wall AS SELECT * FROM '*/wall.csv';
```
*(This includes pulling cross-floor elements from the `global/` folder).*

### B. Dynamic Geometry Injection (The `computed` fields)
The CLI quickly parses the `.svg` files using standard XML parsers. It evaluates geometric relationships and injects them into the DuckDB instance as SQL columns defined as `computed: true` in the YAML schemas.
- `start_x`, `start_y`, `end_x`, `end_y`, `length` extracted from `<line>`.
- `thickness` extracted from SVG `stroke-width`.
- `area` derived from the vertices of `<polygon>`.

**Result:** The LLM gets a `wall` table that looks incredibly rich (containing spatial data, lengths, volumes, etc.) for SQL `SELECT/UPDATE` operations, despite the physical `.csv` only holding 5 semantic columns.

---

## 2. Execution Phase (Standard SQL)

The LLM is now completely abstracted away from the file system. It can safely fire declarative commands like:
> "Find all sliding doors wider than 0.9m on the 1st floor and change them to swing doors."

```sql
UPDATE door SET operation = 'swing' 
WHERE width > 0.9 AND level_id = 'lv-1' AND operation = 'sliding';
```

---

## 3. Sync-Out Phase (Dehydration & Auto-Healing)

The most crucial strategy lies in how DuckDB writes data back to the physical files.

### A. Stripping Computed Fields
When re-partitioning and saving the DuckDB tables back to physical CSVs (e.g. `1F/door.csv`), the CLI engine automatically ignores any columns flagged with `computed: true` in the YAML schema. The CSV remains purely semantic.

### B. Auto-Healing: The "Semantic Source of Truth" Rule
LLMs are mathematically deficient (especially at trigonometry and precise float generation). When generating a door on a slanted wall in SVG, an LLM might mistakenly draw a `<line>` with length `0.887` instead of `<line length="0.9">`.

To prevent "Floating Point / Math Hallucinations":
1. The CSV `door.yaml` defines `width` as `required: true` (a semantic definition), meaning the CSV is the absolute **Source of Truth** for the door size.
2. During Sync-Out, the CLI tool notices the SVG line length (`0.887`) does not match the semantic truth (`0.9`).
3. The parser takes the SVG line's **midpoint** (as the pure intention of *placement/location*).
4. **Auto-corrects** the SVG: The tool re-draws the SVG `<line>` symmetrically around that midpoint so that its length perfectly snaps to `0.9`, and writes to `door.svg`.

This creates an extremely resilient **Twin-Engine**: 
- **CSV enforces Standard Typifications** (e.g., width = 0.9).
- **SVG enforces Spatial Intent** (e.g., placing the center of the door here).
- **DuckDB CLI Engine enforces perfect synchronization** between the two.
