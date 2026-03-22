# BimDown Revit Add-in

A Revit add-in for bidirectional BIM data exchange via CSV files. Export building elements to structured CSV tables, modify them externally, and import changes back into Revit with automatic diff-based synchronization.

## Features

### Export

Exports Revit model elements to CSV files organized by category:

| Category | Tables |
|----------|--------|
| Global | `level`, `grid` |
| Architecture | `wall`, `column`, `slab`, `space`, `door`, `window`, `stair`, `curtain_wall` |
| Structure | `structure_wall`, `structure_column`, `structure_slab`, `beam`, `brace`, `isolated_foundation`, `strip_foundation`, `raft_foundation` |
| MEP | `duct`, `pipe`, `cable_tray`, `conduit`, `mep_node`, `equipment`, `terminal` |

Each CSV uses `element.UniqueId` as the `id` field. Units are metric (meters, square meters, degrees).

### Import

Reads CSV files from a folder and performs a three-way diff against the current model:

- **Update** — CSV row `id` matches an existing element's UniqueId: parameters and geometry are updated
- **Create** — CSV row `id` not found in the model: a new element is created
- **Delete** — model element not present in any CSV row: element is deleted

**Supported element types (v1):** Level, Grid, Wall, Curtain Wall, Column, Slab, Space, Door, Window.

Each table is imported in a separate transaction, so a failure in one table does not affect others. Import order respects dependencies (levels before walls, walls before doors).

#### Type Auto-Creation

When importing elements that require specific family types (wall thickness, column profile, door/window dimensions), the importer automatically resolves or creates types:

- Searches for an existing type with matching parameters
- If not found, duplicates an existing type and modifies its parameters
- Naming convention: `BimDown_{dimension}` (e.g., `BimDown_200mm`, `BimDown_rect_400x400`, `BimDown_900x2100`)

#### Read-Only Fields

The following fields are computed by Revit and ignored during import:
`bbox_min_x/y/z`, `bbox_max_x/y/z`, `volume`, `area`, `length`, `height` (computed), `created_at`, `updated_at`, `material`.

### Batch Export

The `BatchExportApp` (registered as an `IExternalApplication`) supports headless batch export. Place a JSON config at `%TEMP%/bimdown-batch.json`:

```json
{"ModelPath": "C:\\path\\to\\model.rvt", "OutputDir": "C:\\output"}
```

Revit will export all tables on document open and terminate automatically.

## Project Structure

```
revit-addin/
├── ExportCommand.cs          # IExternalCommand for interactive CSV export
├── ImportCommand.cs          # IExternalCommand for interactive CSV import
├── BatchExportApp.cs         # IExternalApplication for headless batch export
├── CsvWriter.cs              # CSV serialization (with quote escaping)
├── CsvReader.cs              # CSV deserialization (symmetric with CsvWriter)
├── UnitConverter.cs           # Bidirectional unit conversion (metric ↔ Revit feet/radians)
├── GeometryUtils.cs           # Bounding box, polygon serialization/deserialization
├── Extractors/                # Field extractors for export (composable pipeline)
│   ├── IFieldExtractor.cs
│   ├── CompositeExtractor.cs
│   ├── ElementExtractor.cs    # Base fields: id, name, level_id, bbox, etc.
│   ├── LineElementExtractor.cs
│   ├── PointElementExtractor.cs
│   ├── PolygonElementExtractor.cs
│   ├── HostedElementExtractor.cs
│   ├── VerticalSpanExtractor.cs
│   ├── SectionProfileExtractor.cs
│   └── ...
├── Tables/                    # Table exporter definitions
│   ├── ITableExporter.cs
│   ├── TableExporter.cs       # Generic exporter using category + extractors
│   ├── GlobalTableExporters.cs
│   ├── ArchitectureTableExporters.cs
│   ├── StructureTableExporters.cs
│   └── MepTableExporters.cs
├── Import/                    # Import framework
│   ├── ITableImporter.cs      # Interface + ImportResult record
│   ├── TableImporterBase.cs   # Abstract base: collect → diff → dispatch
│   ├── DiffEngine.cs          # UniqueId-based three-way diff
│   ├── DiffResult.cs          # ToUpdate / ToCreate / ToDelete
│   ├── TypeResolver.cs        # Auto-create family types by dimension
│   ├── IdMap.cs               # Cross-table ID resolution for references
│   ├── GlobalImporters.cs     # Level, Grid importers
│   ├── ArchitectureImporters.cs # Wall, CurtainWall, Column, Slab, Space, Door, Window importers
│   ├── StructureImporters.cs  # Structure wall, column, slab, beam, brace, foundation importers
│   └── MepImporters.cs        # Duct, pipe, cable tray, conduit, equipment, terminal importers
├── BimDown.addin              # Revit add-in manifest
└── BimDown.RevitAddin.csproj
```

## Building

```bash
dotnet build BimDown.RevitAddin.csproj
# or specify Revit version:
dotnet build BimDown.RevitAddin.csproj -p:RevitVersion=2025
```

Requires the [Nice3point Revit API NuGet packages](https://github.com/Nice3point/RevitApi). Default target is Revit 2026 on .NET 8.

## Installation

Copy `BimDown.addin` to `%AppData%\Autodesk\Revit\Addins\{version}\` and update the `<Assembly>` path to point to the built DLL.
