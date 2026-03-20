using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Svg;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

[Explicit]
[Category("SampleProject")]
public class SampleProjectExportTests : RevitApiTest
{
    const string SamplesDir = @"C:\Program Files\Autodesk\Revit 2026\Samples";

    static readonly string[] ArchitecturalTables = ["level", "grid", "wall", "column", "slab", "space", "door", "window", "stair"];
    static readonly string[] StructuralTables = ["level", "grid", "structure_wall", "structure_column", "structure_slab", "beam", "isolated_foundation", "raft_foundation"];

    static ITableExporter[] AllExporters() =>
    [
        new LevelTableExporter(),
        new GridTableExporter(),
        ArchitectureTableExporters.Wall(),
        ArchitectureTableExporters.Column(),
        ArchitectureTableExporters.Slab(),
        ArchitectureTableExporters.Space(),
        ArchitectureTableExporters.Door(),
        ArchitectureTableExporters.Window(),
        ArchitectureTableExporters.Stair(),
        StructureTableExporters.StructureWall(),
        StructureTableExporters.StructureColumn(),
        StructureTableExporters.StructureSlab(),
        StructureTableExporters.Beam(),
        StructureTableExporters.Brace(),
        StructureTableExporters.IsolatedFoundation(),
        StructureTableExporters.StripFoundation(),
        StructureTableExporters.RaftFoundation(),
        MepTableExporters.Duct(),
        MepTableExporters.Pipe(),
        MepTableExporters.CableTray(),
        MepTableExporters.Conduit(),
        MepTableExporters.Equipment(),
        MepTableExporters.Terminal(),
    ];

    static Document OpenSample(Autodesk.Revit.ApplicationServices.Application app, string fileName)
    {
        var path = Path.Combine(SamplesDir, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Sample project not found: {path}");
        return app.OpenDocumentFile(path);
    }

    static void ExportAndVerify(
        Document doc,
        string[] expectedNonEmptyTables,
        string outputSubDir)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "BimDown_SampleExport", outputSubDir);
        Directory.CreateDirectory(outputDir);

        var exporters = AllExporters();
        var idGen = new ShortIdGenerator();
        var results = new Dictionary<string, int>();
        var errors = new List<string>();

        // Pass 1: export all tables (rows still have UniqueIds)
        var exported = new List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)>();
        foreach (var exporter in exporters)
        {
            try
            {
                var rows = exporter.Export(doc);
                results[exporter.TableName] = rows.Count;
                if (rows.Count > 0)
                    exported.Add((exporter, rows));
            }
            catch (Exception ex)
            {
                errors.Add($"{exporter.TableName}: {ex.Message}");
            }
        }

        // Pass 2: remap IDs to short format
        foreach (var (exporter, rows) in exported)
        {
            idGen.RemapRows(exporter.TableName, rows);

            // Verify all rows have the expected columns
            foreach (var row in rows)
            {
                if (!row.ContainsKey("id"))
                    errors.Add($"{exporter.TableName}: row missing 'id' column");
            }

            // Verify CSV round-trip using CsvColumns (excludes computed fields)
            var (rtColumns, rtRows) = RevitTestHelper.RoundTripCsv(exporter.CsvColumns, rows);
            if (rtRows.Count != rows.Count)
                errors.Add($"{exporter.TableName}: CSV round-trip row count mismatch ({rows.Count} -> {rtRows.Count})");
        }

        // Build level_id → short_id map
        var levelIdToShortId = new Dictionary<string, string>();
        var levelEntry = exported.FirstOrDefault(e => e.Exporter.TableName == "level");
        if (levelEntry.Rows is not null)
        {
            foreach (var row in levelEntry.Rows)
            {
                if (row.TryGetValue("id", out var id) && id is not null)
                    levelIdToShortId[id] = id;
            }
        }

        // Write level-partitioned CSVs
        var globalTableNames = new HashSet<string> { "level", "grid" };
        foreach (var (exporter, rows) in exported)
        {
            if (globalTableNames.Contains(exporter.TableName))
            {
                var globalDir = Path.Combine(outputDir, "global");
                Directory.CreateDirectory(globalDir);
                CsvWriter.Write(Path.Combine(globalDir, $"{exporter.TableName}.csv"), exporter.CsvColumns, rows);
            }
            else
            {
                var grouped = rows.GroupBy(r => r.GetValueOrDefault("level_id"));
                foreach (var group in grouped)
                {
                    string dirName;
                    if (group.Key is not null && levelIdToShortId.TryGetValue(group.Key, out var shortId))
                        dirName = shortId;
                    else if (group.Key is not null)
                        dirName = group.Key;
                    else
                        dirName = "global";

                    var dir = Path.Combine(outputDir, dirName);
                    Directory.CreateDirectory(dir);
                    CsvWriter.Write(Path.Combine(dir, $"{exporter.TableName}.csv"), exporter.CsvColumns, group.ToList());
                }
            }
        }

        // Write _IdMap.csv
        var globalFolder = Path.Combine(outputDir, "global");
        Directory.CreateDirectory(globalFolder);
        var idMapRows = idGen.Mappings.Select(kvp => new Dictionary<string, string?>
        {
            ["id"] = kvp.Value,
            ["uuid"] = kvp.Key
        }).ToList();
        CsvWriter.Write(Path.Combine(globalFolder, "_IdMap.csv"), ["id", "uuid"], idMapRows);

        // Pass 3: write SVG geometry layer
        var levelData = exported.FirstOrDefault(e => e.Exporter.TableName == "level");
        if (levelData.Rows is not null)
        {
            try
            {
                SvgWriter.WriteAll(outputDir,
                    exported.Select(e => (e.Exporter.TableName, e.Rows)).ToList(),
                    levelData.Rows);
            }
            catch (Exception ex)
            {
                errors.Add($"SVG export: {ex.Message}");
            }
        }

        // Verify expected tables produced data
        foreach (var table in expectedNonEmptyTables)
        {
            if (!results.TryGetValue(table, out var count) || count == 0)
                errors.Add($"Expected non-empty table '{table}' but got 0 rows");
        }

        if (errors.Count > 0)
            throw new Exception(
                $"Export verification failed for {outputSubDir}:\n" +
                string.Join("\n", errors) +
                $"\n\nAll results: {string.Join(", ", results.Select(r => $"{r.Key}={r.Value}"))}");
    }

    [Test]
    public async Task Export_Architectural_SnowdonTowers()
    {
        var doc = OpenSample(Application, "Snowdon Towers Sample Architectural.rvt");
        try
        {
            ExportAndVerify(doc, ArchitecturalTables, "architectural");

            var levelExporter = new LevelTableExporter();
            var levels = levelExporter.Export(doc);
            await Assert.That(levels.Count).IsGreaterThanOrEqualTo(2);

            var wallExporter = ArchitectureTableExporters.Wall();
            var walls = wallExporter.Export(doc);
            await Assert.That(walls.Count).IsGreaterThan(0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Export_Structural_SnowdonTowers()
    {
        var doc = OpenSample(Application, "Snowdon Towers Sample Structural.rvt");
        try
        {
            ExportAndVerify(doc, StructuralTables, "structural");

            var beamExporter = StructureTableExporters.Beam();
            var beams = beamExporter.Export(doc);
            await Assert.That(beams.Count).IsGreaterThan(0);

            var columnExporter = StructureTableExporters.StructureColumn();
            var columns = columnExporter.Export(doc);
            await Assert.That(columns.Count).IsGreaterThan(0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Export_HVAC_SnowdonTowers()
    {
        var doc = OpenSample(Application, "Snowdon Towers Sample HVAC.rvt");
        try
        {
            ExportAndVerify(doc, ["level", "duct", "equipment", "terminal"], "hvac");

            var ductExporter = MepTableExporters.Duct();
            var ducts = ductExporter.Export(doc);
            await Assert.That(ducts.Count).IsGreaterThan(0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Export_Electrical_SnowdonTowers()
    {
        var doc = OpenSample(Application, "Snowdon Towers Sample Electrical.rvt");
        try
        {
            ExportAndVerify(doc, ["level", "conduit", "equipment", "terminal"], "electrical");

            var conduitExporter = MepTableExporters.Conduit();
            var conduits = conduitExporter.Export(doc);
            await Assert.That(conduits.Count).IsGreaterThan(0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Export_Plumbing_SnowdonTowers()
    {
        var doc = OpenSample(Application, "Snowdon Towers Sample Plumbing.rvt");
        try
        {
            ExportAndVerify(doc, ["level", "pipe", "terminal"], "plumbing");

            var pipeExporter = MepTableExporters.Pipe();
            var pipes = pipeExporter.Export(doc);
            await Assert.That(pipes.Count).IsGreaterThan(0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Export_AllProjects_NumericFieldsAreValid()
    {
        string[] sampleFiles =
        [
            "Snowdon Towers Sample Architectural.rvt",
            "Snowdon Towers Sample Structural.rvt",
            "Snowdon Towers Sample HVAC.rvt",
        ];

        string[] numericFields = ["elevation", "start_x", "start_y", "end_x", "end_y", "height", "thickness", "width", "rotation", "length", "step_count"];

        var errors = new List<string>();

        foreach (var file in sampleFiles)
        {
            var doc = OpenSample(Application, file);
            try
            {
                foreach (var exporter in AllExporters())
                {
                    List<Dictionary<string, string?>> rows;
                    try { rows = exporter.Export(doc); }
                    catch { continue; }

                    foreach (var row in rows)
                    {
                        foreach (var field in numericFields)
                        {
                            if (!row.TryGetValue(field, out var val) || val is null) continue;
                            if (!double.TryParse(val, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var num))
                                errors.Add($"{file}/{exporter.TableName}: '{field}' = '{val}' is not a valid number");
                            else if (!double.IsFinite(num))
                                errors.Add($"{file}/{exporter.TableName}: '{field}' = '{val}' is not finite");
                        }
                    }
                }
            }
            finally
            {
                doc.Close(false);
            }
        }

        await Assert.That(errors.Count).IsEqualTo(0);
    }
}
