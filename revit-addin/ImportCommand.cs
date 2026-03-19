using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimDown.RevitAddin.Import;

namespace BimDown.RevitAddin;

[Transaction(TransactionMode.Manual)]
public class ImportCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select folder containing CSV files for import",
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return Result.Cancelled;

        var inputDir = dialog.SelectedPath;
        var idMap = new IdMap();

        TableImporterBase[] importers =
        [
            // Order 0
            new LevelImporter(),
            // Order 1
            new GridImporter(),
            // Order 10
            new WallImporter(),
            new StructureWallImporter(),
            new ColumnImporter(),
            new StructureColumnImporter(),
            // Order 15
            new SlabImporter(),
            new StructureSlabImporter(),
            new SpaceImporter(),
            new BeamImporter(),
            new BraceImporter(),
            new IsolatedFoundationImporter(),
            new StripFoundationImporter(),
            new RaftFoundationImporter(),
            new StairImporter(),
            // Order 20
            new DoorImporter(),
            new WindowImporter(),
            // Order 25
            new DuctImporter(),
            new PipeImporter(),
            new CableTrayImporter(),
            new ConduitImporter(),
            // Order 30
            new EquipmentImporter(),
            new TerminalImporter(),
        ];

        foreach (var importer in importers)
            importer.SetIdMap(idMap);

        // Sort by order (already ordered in the array, but be explicit)
        var sorted = importers.OrderBy(i => i.Order).ToArray();

        var totalCreated = 0;
        var totalUpdated = 0;
        var totalDeleted = 0;
        var allErrors = new List<string>();
        var tablesProcessed = 0;

        foreach (var importer in sorted)
        {
            var csvPath = Path.Combine(inputDir, $"{importer.TableName}.csv");
            if (!File.Exists(csvPath)) continue;

            try
            {
                var (_, rows) = CsvReader.Read(csvPath);
                if (rows.Count == 0) continue;

                using var tx = new Transaction(doc, $"BimDown Import: {importer.TableName}");
                tx.Start();

                try
                {
                    var result = importer.Import(doc, rows);
                    tx.Commit();

                    totalCreated += result.Created;
                    totalUpdated += result.Updated;
                    totalDeleted += result.Deleted;
                    tablesProcessed++;

                    foreach (var error in result.Errors)
                        allErrors.Add($"[{importer.TableName}] {error}");
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    allErrors.Add($"[{importer.TableName}] Transaction failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"[{importer.TableName}] Read failed: {ex.Message}");
            }
        }

        var msg = $"Import complete ({tablesProcessed} tables):\n" +
                  $"  Created: {totalCreated}\n" +
                  $"  Updated: {totalUpdated}\n" +
                  $"  Deleted: {totalDeleted}";

        if (allErrors.Count > 0)
            msg += $"\n\nErrors ({allErrors.Count}):\n" + string.Join("\n", allErrors);

        Autodesk.Revit.UI.TaskDialog.Show("BimDown Import", msg);
        return Result.Succeeded;
    }
}
