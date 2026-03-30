# LLM-Based Classification for BimDown Export

## Problem

When exporting from Revit to BimDown CSV, raw Revit strings (material names, family names, parameter names) must be mapped to BimDown's spec-defined enum values and field names. Hardcoded string matching rules cannot cover all cases because:

- Material names vary by language, vendor, and project conventions
- Family parameter names are user-defined (e.g. `b`/`h` vs `Width`/`Depth` vs `宽`/`高`)
- Equipment and terminal types can only be inferred from family/type names
- Door operation types depend on family naming conventions

## Design

### Classification Dimensions

| Dimension | Granularity | Source (Revit) | Target (BimDown) | Current Implementation |
|-----------|-------------|----------------|-------------------|----------------------|
| Material name | per Material | `Material.Name` | `material` enum | `MaterializedExtractor` hardcoded `contains()` |
| Family param names | per Family | Parameter `.Name` | BimDown fields (`size_x`, `width`, `height`...) | `ParameterUtils.FindDoubleParameterByNames` hardcoded list |
| Equipment type | per Family | Family name / type name | `equipment_type` enum | null (not implemented) |
| Terminal type | per Family | Family name / type name | `terminal_type` enum | null (not implemented) |
| Door operation | per Family | Family name / param values | `operation` enum | `GetDoorOperation` hardcoded rules |
| Roof type | per Element | Slope value | `roof_type` enum | Simple slope check (0=flat, else gable) |

Key insight: **Material** is a shared Revit entity (not per-family), so it needs its own lookup table. All other classifications are per-family since elements of the same family share parameter structure.

### Two-Phase Workflow

```
Phase 1: Export (Revit process)
  ├─ Load _classification_cache.json (if exists)
  ├─ Export all tables
  │   ├─ For each field: check cache → fallback to hardcoded rules
  │   └─ Collect unknown materials and unmapped families into hints
  ├─ Write CSVs, SVGs, metadata
  └─ Write _classification_hints.json (unknowns for LLM)

Phase 2: Classify (CLI, outside Revit)
  ├─ Read _classification_hints.json
  ├─ Build LLM prompt (batch all unknowns in one call)
  ├─ Call LLM API → structured JSON response
  ├─ Merge results into _classification_cache.json
  └─ User re-exports from Revit (cache now covers previously unknown families)
```

### File Locations

Both files live in the **project root directory** (alongside `project_metadata.json`):

```
{project}/
├── project_metadata.json
├── _classification_cache.json    # Persistent cache (committed to repo)
├── _classification_hints.json    # Temporary (generated on export, consumed by classify)
├── global/
├── lv-1/
└── ...
```

---

## Cache File: `_classification_cache.json`

```json
{
  "materials": {
    "C30 Concrete": "concrete",
    "Generic - Interior Partition": "gypsum",
    "Aluminum Frame": "aluminum",
    "Custom Facade Panel": "composite"
  },
  "families": {
    "column::Concrete-Rectangular Column": {
      "params": {
        "size_x": "b",
        "size_y": "h"
      }
    },
    "door::Single-Flush": {
      "params": {
        "width": "Width",
        "height": "Height"
      },
      "enums": {
        "operation": "single_swing"
      }
    },
    "equipment::M_AHU - Horizontal": {
      "enums": {
        "equipment_type": "ahu"
      }
    },
    "terminal::M_Supply Diffuser - Perforated": {
      "enums": {
        "terminal_type": "supply_air_diffuser"
      }
    },
    "window::Fixed Window": {
      "params": {
        "width": "Width",
        "height": "Height"
      }
    }
  }
}
```

- `materials`: Revit material name (string) → BimDown enum value
- `families`: key is `{bimdown_category}::{revit_family_name}`
  - `params`: BimDown field name → Revit parameter name (for numeric/string value extraction)
  - `enums`: BimDown field name → BimDown enum value (classification from family identity)

### Cache Behavior

- Cache is **additive** — new entries are merged, existing entries are never overwritten automatically
- Users can **manually edit** the cache to correct LLM mistakes; manual edits survive re-classification
- Cache is **project-scoped** — different projects may have different family libraries

---

## Hints File: `_classification_hints.json`

Generated during export for entries not found in cache:

```json
{
  "materials": [
    {
      "name": "Unknown Custom Material",
      "used_by_categories": ["wall", "slab"]
    },
    {
      "name": "Precast - Exposed Aggregate",
      "used_by_categories": ["structure_wall"]
    }
  ],
  "families": [
    {
      "key": "column::Custom Column Family",
      "category": "column",
      "family_name": "Custom Column Family",
      "type_names": ["300x500", "400x600"],
      "instance_params": [
        { "name": "b", "type": "double", "sample": "0.3" },
        { "name": "h", "type": "double", "sample": "0.5" },
        { "name": "Cover", "type": "double", "sample": "0.04" }
      ],
      "type_params": [
        { "name": "Concrete Grade", "type": "string", "sample": "C30" },
        { "name": "Mark", "type": "string", "sample": "C1" }
      ],
      "fields_needed": {
        "params": [
          { "field": "size_x", "description": "Cross-section width (meters)" },
          { "field": "size_y", "description": "Cross-section depth (meters)" }
        ],
        "enums": []
      }
    },
    {
      "key": "equipment::Custom Chiller Unit",
      "category": "equipment",
      "family_name": "Custom Chiller Unit",
      "type_names": ["100kW", "200kW"],
      "instance_params": [
        { "name": "Capacity", "type": "double", "sample": "100" }
      ],
      "type_params": [],
      "fields_needed": {
        "params": [],
        "enums": [
          {
            "field": "equipment_type",
            "values": ["ahu", "fcu", "chiller", "boiler", "cooling_tower", "fan", "pump", "transformer", "panelboard", "generator", "water_heater", "tank", "other"]
          }
        ]
      }
    }
  ],
  "element_families": {
    "c-1": "column::Custom Column Family",
    "c-2": "column::Custom Column Family",
    "eq-1": "equipment::Custom Chiller Unit"
  }
}
```

- `element_families` maps element IDs to their family key, enabling CLI to know which cache entry applies to which CSV row (for potential future CLI-side backfill)

---

## Fields Needing Classification Per Category

| BimDown Category | param mapping fields | enum classification fields |
|-----------------|---------------------|--------------------------|
| wall | thickness* | material |
| structure_wall | thickness* | material |
| column | size_x, size_y | material |
| structure_column | size_x, size_y | material |
| slab | thickness | material |
| structure_slab | thickness | material |
| door | width, height | material, operation |
| window | width, height | material |
| beam | size_x, size_y | material |
| brace | size_x, size_y | material |
| foundation (isolated) | length, width, thickness | material |
| curtain_wall | — | material, panel_material |
| stair | width | — |
| equipment | — | equipment_type |
| terminal | — | terminal_type |
| roof | thickness | material, roof_type** |

*Wall thickness comes from `Wall.Width` API, not a named parameter — no mapping needed.
**Roof type may need LLM for complex roof forms (hip, mansard, shed).

---

## Revit Plugin Changes

### New: `ClassificationCache.cs`

```csharp
class ClassificationCache
{
    Dictionary<string, string> Materials;      // raw_name → enum
    Dictionary<string, FamilyMapping> Families; // "category::family" → mapping

    static ClassificationCache Load(string projectDir);
    void Save(string projectDir);

    string? GetMaterial(string rawMaterialName);
    string? GetParamName(string category, string familyName, string field);
    string? GetEnumValue(string category, string familyName, string field);
    bool HasFamily(string category, string familyName);
}

record FamilyMapping(
    Dictionary<string, string>? Params,   // bimdown_field → revit_param_name
    Dictionary<string, string>? Enums);   // bimdown_field → enum_value
```

### New: `ClassificationCollector.cs`

```csharp
class ClassificationCollector
{
    Dictionary<string, HashSet<string>> UnknownMaterials; // name → categories
    Dictionary<string, FamilyHint> UnknownFamilies;       // key → hint
    Dictionary<string, string> ElementFamilies;            // element_id → family_key

    void RecordMaterial(string rawName, string category);
    void RecordFamily(Element element, string category, string[] paramFields, string[] enumFields);
    void RecordElementFamily(string elementId, string category, string familyName);
    void WriteHints(string projectDir);
}
```

### Modified Extractors

Each extractor that does string mapping receives `ClassificationCache` and `ClassificationCollector` via a shared context object:

```csharp
class ExportContext
{
    public ClassificationCache Cache { get; }
    public ClassificationCollector Collector { get; }
}
```

Extractors are modified to:
1. Check cache first
2. Fall back to existing hardcoded rules
3. Record unknowns to collector

Example — `MaterializedExtractor`:

```csharp
public Dictionary<string, string?> Extract(Element element)
{
    var rawName = GetRawMaterialName(element);
    if (rawName is null) return new() { ["material"] = "composite" };

    // 1. Cache hit
    var cached = _context.Cache.GetMaterial(rawName);
    if (cached is not null) return new() { ["material"] = cached };

    // 2. Hardcoded fallback
    var fallback = MapToEnum(rawName);

    // 3. Record if fallback is uncertain
    if (fallback == "composite")
        _context.Collector.RecordMaterial(rawName, _currentCategory);

    return new() { ["material"] = fallback };
}
```

Example — `SectionProfileExtractor` (for columns):

```csharp
public Dictionary<string, string?> Extract(Element element)
{
    var familyName = GetFamilyName(element);
    var category = _currentCategory;

    // 1. Try cache for param name mapping
    var sizeXParam = _context.Cache.GetParamName(category, familyName, "size_x");
    var sizeYParam = _context.Cache.GetParamName(category, familyName, "size_y");

    double? sizeX = null, sizeY = null;
    if (sizeXParam is not null)
        sizeX = element.LookupParameter(sizeXParam)?.AsDouble();
    if (sizeYParam is not null)
        sizeY = element.LookupParameter(sizeYParam)?.AsDouble();

    // 2. Fallback to existing search
    sizeX ??= FindDoubleParameterByNames(element, "width", "w", "b", "宽");
    sizeY ??= FindDoubleParameterByNames(element, "height", "depth", "h", "d", "高", "深");

    // 3. Record unknown family if either is still null
    if ((sizeX is null || sizeY is null) && !_context.Cache.HasFamily(category, familyName))
        _context.Collector.RecordFamily(element, category, ["size_x", "size_y"], []);

    return new()
    {
        ["shape"] = DetectShape(element),
        ["size_x"] = sizeX is { } x ? FormatDouble(Length(x)) : null,
        ["size_y"] = sizeY is { } y ? FormatDouble(Length(y)) : null,
    };
}
```

### Modified ExportCommand

```csharp
public Result Execute(...)
{
    // ... setup ...

    // Load classification cache
    var cache = ClassificationCache.Load(outputDir);
    var collector = new ClassificationCollector();
    var exportContext = new ExportContext(cache, collector);

    // Pass extractors the context (via constructor or thread-static)
    // ... export all tables ...

    // Write outputs
    WriteCsvs(...);
    WriteSvgs(...);
    WriteMetadata(...);
    collector.WriteHints(outputDir);  // Write _classification_hints.json

    // ... write IDs to model ...
}
```

---

## CLI Changes

### New Command: `bimdown classify <project-dir>`

```
1. Read _classification_hints.json
   - If not found or empty → print "Nothing to classify" and exit

2. Read _classification_cache.json (if exists)
   - Filter out hints that are already in cache

3. Build LLM prompt
   - Group materials and families into a single structured prompt
   - Include enum value options for each field
   - Request JSON output

4. Call LLM API (Anthropic Claude)
   - Single API call for all unknowns
   - Parse structured JSON response

5. Merge results into _classification_cache.json
   - Only add new entries, never overwrite existing (user edits are preserved)

6. Delete _classification_hints.json

7. Print summary: "Classified X materials, Y families. Re-export from Revit to apply."
```

### LLM Prompt Template

```
You are a BIM data classification assistant. Given Revit model metadata, map raw
strings to BimDown schema values.

## Task 1: Material Classification

Map each Revit material name to ONE of these values:
concrete, steel, wood, clt, glass, aluminum, brick, stone, gypsum, insulation,
copper, pvc, ceramic, fiber_cement, composite

Materials to classify:
| Material Name | Used By |
|---|---|
| Generic - Interior Partition | wall, slab |
| Precast - Exposed Aggregate | structure_wall |

## Task 2: Family Parameter Mapping

For each Revit family, determine which parameter corresponds to each BimDown field.

### Family: "Custom Column" (category: column)
Instance params: b(double, sample=0.3), h(double, sample=0.5), Cover(double, sample=0.04)
Type params: Grade(string, sample="C30"), Mark(string, sample="C1")

Map these fields (choose the best matching parameter name, or null if none):
- size_x: cross-section width in meters
- size_y: cross-section depth in meters

### Family: "M_AHU - Horizontal" (category: equipment)
Instance params: Air Flow(double, sample=2.5), Width(double, sample=0.5)

Classify (choose one value):
- equipment_type: [ahu, fcu, chiller, boiler, cooling_tower, fan, pump,
  transformer, panelboard, generator, water_heater, tank, other]

## Response Format

Return JSON only:
{
  "materials": {
    "Generic - Interior Partition": "gypsum",
    "Precast - Exposed Aggregate": "concrete"
  },
  "families": {
    "column::Custom Column": {
      "params": { "size_x": "b", "size_y": "h" },
      "enums": {}
    },
    "equipment::M_AHU - Horizontal": {
      "params": {},
      "enums": { "equipment_type": "ahu" }
    }
  }
}
```

---

## Open Questions

1. **LLM Backend**: Call Anthropic API directly from CLI, or route through a custom backend service?
2. **Re-export vs CLI backfill**: After `classify`, user re-exports from Revit (simpler, cache is applied). Or CLI rewrites CSVs directly (no re-export needed, but requires `element_families` mapping)?
3. **Batch size**: If a project has hundreds of unknown families, should we split into multiple LLM calls?
4. **Confidence**: Should the LLM response include confidence? Should low-confidence results be flagged for user review?
