using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
            // Structure
            StructureTableExporters.StructureWall(),
            StructureTableExporters.StructureColumn(),
            StructureTableExporters.StructureSlab(),
            StructureTableExporters.Beam(),
            StructureTableExporters.Brace(),
            StructureTableExporters.IsolatedFoundation(),
            StructureTableExporters.StripFoundation(),
            StructureTableExporters.RaftFoundation(),
            // MEP
            MepTableExporters.Duct(),
            MepTableExporters.Pipe(),
            MepTableExporters.CableTray(),
            MepTableExporters.Conduit(),
            MepTableExporters.Equipment(),
            MepTableExporters.Terminal(),
        ];

        var idGen = new ShortIdGenerator();
        var errors = new List<string>();

        // Ensure BimDown_Id shared parameter exists
        using (var txParam = new Transaction(doc, "BimDown: Ensure parameter"))
        {
            txParam.Start();
            try
            {
                BimDownParameter.EnsureParameter(doc);
                txParam.Commit();
            }
            catch (Exception ex)
            {
                txParam.RollBack();
                errors.Add($"Parameter setup: {ex.Message}");
            }
        }

        // Seed from existing BimDown_Id values on model elements
        foreach (var category in BimDownParameter.AllCategories)
        {
            try
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();
                idGen.SeedFromModel(collector.OrderBy(e => e.Id.Value).ToList());
            }
            catch { }
        }

        // Pass 1: export all tables (rows still have UniqueIds)
        var exported = new List<(ITableExporter Exporter, List<Dictionary<string, string?>> Rows)>();
        foreach (var exporter in exporters)
        {
            try
            {
                var rows = exporter.Export(doc);
                if (rows.Count == 0) continue;
                exported.Add((exporter, rows));
            }
            catch (Exception ex)
            {
                errors.Add($"{exporter.TableName}: {ex.Message}");
            }
        }

        // Pass 2: remap IDs and write CSVs
        foreach (var (exporter, rows) in exported)
        {
            idGen.RemapRows(exporter.TableName, rows);
            var filePath = Path.Combine(outputDir, $"{exporter.TableName}.csv");
            CsvWriter.Write(filePath, exporter.Columns, rows);
        }

        // Write BimDown_Id back to Revit elements
        using (var txWrite = new Transaction(doc, "BimDown: Write short IDs"))
        {
            txWrite.Start();
            try
            {
                foreach (var (uniqueId, shortId) in idGen.Mappings)
                {
                    var element = doc.GetElement(uniqueId);
                    if (element is not null)
                        BimDownParameter.Set(element, shortId);
                }
                txWrite.Commit();
            }
            catch (Exception ex)
            {
                txWrite.RollBack();
                errors.Add($"Write IDs: {ex.Message}");
            }
        }

        var msg = $"Exported {exported.Count} tables to:\n{outputDir}";
        if (errors.Count > 0)
            msg += $"\n\nWarnings ({errors.Count}):\n" + string.Join("\n", errors);

        Autodesk.Revit.UI.TaskDialog.Show("BimDown Export", msg);
        return Result.Succeeded;
    }
}
