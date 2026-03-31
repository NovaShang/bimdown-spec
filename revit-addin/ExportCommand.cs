using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimDown.RevitAddin.Svg;
using BimDown.RevitAddin.Tables;

namespace BimDown.RevitAddin;

[Transaction(TransactionMode.Manual)]
public class ExportCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select output folder for CSV export",
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return Result.Cancelled;

        var outputDir = dialog.SelectedPath;

        ITableExporter[] exporters =
        [
            // Global
            new LevelTableExporter(),
            new GridTableExporter(),
            // Architecture
            ArchitectureTableExporters.Wall(),
            ArchitectureTableExporters.Column(),
            ArchitectureTableExporters.Slab(),
            ArchitectureTableExporters.Space(),
            ArchitectureTableExporters.Door(),
            ArchitectureTableExporters.Window(),
            ArchitectureTableExporters.Stair(),
            ArchitectureTableExporters.CurtainWall(),
            ArchitectureTableExporters.Roof(),
            ArchitectureTableExporters.Ceiling(),
            ArchitectureTableExporters.Opening(),
            ArchitectureTableExporters.Ramp(),
            ArchitectureTableExporters.Railing(),
            ArchitectureTableExporters.RoomSeparator(),
            // Structure
            StructureTableExporters.StructureWall(),
            StructureTableExporters.StructureColumn(),
            StructureTableExporters.StructureSlab(),
            StructureTableExporters.Beam(),
            StructureTableExporters.Brace(),
            StructureTableExporters.Foundation(),
            // MEP
            MepTableExporters.Duct(),
            MepTableExporters.Pipe(),
            MepTableExporters.CableTray(),
            MepTableExporters.Conduit(),
            MepTableExporters.MepNode(),
            MepTableExporters.Equipment(),
            MepTableExporters.Terminal(),
            // Mesh fallback
            new MeshExporter(),
        ];

        var idGen = new ShortIdGenerator();
        var errors = new List<string>();

        EnsureParameter(doc, errors);
        SeedIds(doc, idGen, errors);
        var exported = ExportTables(doc, exporters, errors);
        RemapIds(exported, idGen);
        var levelIndex = BuildLevelIndex(exported);
        WriteCsvs(outputDir, exported, levelIndex, errors);
        WriteIdMap(outputDir, idGen);
        ExportMeshFiles(outputDir, exported, idGen, errors);
        WriteSvgs(outputDir, exported, errors);
        WriteMetadata(outputDir, doc, errors);
        WriteIdsToModel(doc, idGen, errors);

        var msg = $"Exported {exported.Count} tables to:\n{outputDir}";
        if (errors.Count > 0)
            msg += $"\n\nWarnings ({errors.Count}):\n" + string.Join("\n", errors);

        Autodesk.Revit.UI.TaskDialog.Show("BimDown Export", msg);
        return Result.Succeeded;
    }

    static void EnsureParameter(Document doc, List<string> errors)
    {
        using var tx = new Transaction(doc, "BimDown: Ensure parameter");
        tx.Start();
        try
        {
            BimDownParameter.EnsureParameter(doc);
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.RollBack();
            errors.Add($"Parameter setup: {ex.Message}");
        }
    }

    static void SeedIds(Document doc, ShortIdGenerator idGen, List<string> errors)
    {
        foreach (var category in BimDownParameter.AllCategories)
        {
            RunStep($"ID seed ({category})", errors, () =>
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();
                idGen.SeedFromModel(collector.OrderBy(e => e.Id.Value).ToList());
            });
        }
    }

    static List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)> ExportTables(
        Document doc, ITableExporter[] exporters, List<string> errors)
    {
        var exported = new List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)>();
        foreach (var exporter in exporters)
        {
            RunStep(exporter.TableName, errors, () =>
            {
                var rows = exporter.Export(doc);
                if (rows.Count > 0)
                    exported.Add((exporter, rows));
            });
        }
        return exported;
    }

    static void RemapIds(
        List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)> exported,
        ShortIdGenerator idGen)
    {
        foreach (var (exporter, rows) in exported)
            idGen.RemapRows(exporter.TableName, rows);
    }

    static Dictionary<string, int> BuildLevelIndex(
        List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)> exported)
    {
        var levelIndex = new Dictionary<string, int>();
        var levelData = exported.FirstOrDefault(e => e.Exporter.TableName == "level");
        if (levelData.Rows is null) return levelIndex;

        var sorted = levelData.Rows
            .Where(r => r.GetValueOrDefault("id") is not null && r.GetValueOrDefault("elevation") is not null)
            .OrderBy(r => double.Parse(r["elevation"]!, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        for (var i = 0; i < sorted.Count; i++)
            levelIndex[sorted[i]["id"]!] = i;

        return levelIndex;
    }

    static void WriteCsvs(string outputDir,
        List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)> exported,
        Dictionary<string, int> levelIndex, List<string> errors)
    {
        foreach (var (exporter, rows) in exported)
        {
            if (exporter.IsGlobal)
            {
                var globalDir = Path.Combine(outputDir, "global");
                Directory.CreateDirectory(globalDir);
                var filePath = Path.Combine(globalDir, $"{exporter.TableName}.csv");
                CsvWriter.Write(filePath, exporter.CsvColumns, rows);
            }
            else
            {
                WriteLevelPartitionedCsv(outputDir, exporter, rows, levelIndex);
            }
        }
    }

    static void WriteLevelPartitionedCsv(string outputDir, ITableExporter exporter,
        List<Dictionary<string, string?>> rows, Dictionary<string, int> levelIndex)
    {
        var levelRows = new Dictionary<string, List<Dictionary<string, string?>>>();

        foreach (var row in rows)
        {
            var levelId = row.GetValueOrDefault("level_id");
            var topLevelId = row.GetValueOrDefault("top_level_id");

            var isMultiStory = false;
            if (levelId is not null && topLevelId is not null
                && levelIndex.TryGetValue(levelId, out var baseIdx)
                && levelIndex.TryGetValue(topLevelId, out var topIdx))
            {
                isMultiStory = topIdx - baseIdx > 1;
            }

            var dirName = (isMultiStory || levelId is null) ? "global" : levelId;

            if (!levelRows.TryGetValue(dirName, out var list))
            {
                list = [];
                levelRows[dirName] = list;
            }
            list.Add(row);
        }

        foreach (var (dirName, groupRows) in levelRows)
        {
            var dir = Path.Combine(outputDir, dirName);
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"{exporter.TableName}.csv");
            CsvWriter.Write(filePath, exporter.CsvColumns, groupRows);
        }
    }

    static void WriteIdMap(string outputDir, ShortIdGenerator idGen)
    {
        var idMapRows = idGen.Mappings.Select(kvp => new Dictionary<string, string?>
        {
            ["id"] = kvp.Value,
            ["uuid"] = kvp.Key
        }).ToList();
        CsvWriter.Write(Path.Combine(outputDir, "_IdMap.csv"), ["id", "uuid"], idMapRows);
    }

    static void ExportMeshFiles(string outputDir,
        List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)> exported,
        ShortIdGenerator idGen, List<string> errors)
    {
        var meshData = exported.FirstOrDefault(e => e.Exporter is MeshExporter);
        if (meshData.Exporter is MeshExporter meshExporter && meshData.Rows is not null)
        {
            RunStep("GLB export", errors, () =>
                meshExporter.ExportGlbFiles(outputDir, meshData.Rows, idGen.Mappings));
        }
    }

    static void WriteSvgs(string outputDir,
        List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)> exported,
        List<string> errors)
    {
        var levelData = exported.FirstOrDefault(e => e.Exporter.TableName == "level");
        if (levelData.Rows is null) return;

        RunStep("SVG export", errors, () =>
            SvgWriter.WriteAll(outputDir,
                exported.Select(e => (e.Exporter.TableName, e.Rows)).ToList(),
                levelData.Rows));
    }

    static void WriteMetadata(string outputDir, Document doc, List<string> errors)
    {
        RunStep("Metadata", errors, () =>
        {
            var metadata = new Dictionary<string, string>
            {
                ["format_version"] = "3.0",
                ["project_name"] = doc.Title ?? "",
                ["units"] = "meters",
                ["source"] = $"Revit {doc.Application.VersionNumber}"
            };
            var json = System.Text.Json.JsonSerializer.Serialize(metadata,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(outputDir, "project_metadata.json"), json);
        });
    }

    static void WriteIdsToModel(Document doc, ShortIdGenerator idGen, List<string> errors)
    {
        using var tx = new Transaction(doc, "BimDown: Write short IDs");
        tx.Start();
        try
        {
            foreach (var (uniqueId, shortId) in idGen.Mappings)
            {
                var element = doc.GetElement(uniqueId);
                if (element is not null)
                    BimDownParameter.Set(element, shortId);
            }
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.RollBack();
            errors.Add($"Write IDs: {ex.Message}");
        }
    }

    static void RunStep(string name, List<string> errors, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            errors.Add($"{name}: {ex.Message}");
        }
    }
}
