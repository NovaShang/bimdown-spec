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

        var exportedCount = 0;
        var errors = new List<string>();

        foreach (var exporter in exporters)
        {
            try
            {
                var rows = exporter.Export(doc);
                if (rows.Count == 0) continue;

                var filePath = Path.Combine(outputDir, $"{exporter.TableName}.csv");
                CsvWriter.Write(filePath, exporter.Columns, rows);
                exportedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{exporter.TableName}: {ex.Message}");
            }
        }

        var msg = $"Exported {exportedCount} tables to:\n{outputDir}";
        if (errors.Count > 0)
            msg += $"\n\nWarnings ({errors.Count}):\n" + string.Join("\n", errors);

        Autodesk.Revit.UI.TaskDialog.Show("BimDown Export", msg);
        return Result.Succeeded;
    }
}
