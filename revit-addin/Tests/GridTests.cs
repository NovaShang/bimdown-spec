using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class GridTests : RevitApiTest
{
    [Test]
    public async Task Import_Grid_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var importer = new GridImporter();
            var idMap = new IdMap();
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-grid-001",
                    ["number"] = "A",
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "10",
                    ["end_y"] = "0",
                }
            };

            using var tx = new Transaction(doc, "Test Grid Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var gridId = idMap.Resolve(doc, "test-grid-001");
            await Assert.That(gridId).IsNotNull();

            var grid = doc.GetElement(gridId!) as Grid;
            await Assert.That(grid).IsNotNull();
            await Assert.That(grid!.Name).IsEqualTo("A");

            doc.Regenerate();
            var curve = grid.Curve;
            await Assert.That(curve).IsNotNull();
            var start = curve!.GetEndPoint(0);
            var end = curve.GetEndPoint(1);

            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), start.X, 1e-3, "start_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), start.Y, 1e-3, "start_y");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(10), end.X, 1e-3, "end_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), end.Y, 1e-3, "end_y");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Grid_ExportThenImport()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txCreate = new Transaction(doc, "Create Test Grid");
            txCreate.Start();
            var line = Line.CreateBound(
                new XYZ(UnitConverter.LengthToFeet(5), UnitConverter.LengthToFeet(0), 0),
                new XYZ(UnitConverter.LengthToFeet(5), UnitConverter.LengthToFeet(20), 0));
            var grid = Grid.Create(doc, line);
            grid.Name = "B1";
            txCreate.Commit();

            try
            {
                var exporter = new GridTableExporter();
                var exportedRows = exporter.Export(doc);
                var targetRow = exportedRows.Find(r => r["number"] == "B1");
                await Assert.That(targetRow).IsNotNull();

                var exportedStartX = UnitConverter.ParseDouble(targetRow!["start_x"]!);
                var exportedStartY = UnitConverter.ParseDouble(targetRow["start_y"]!);
                var exportedEndX = UnitConverter.ParseDouble(targetRow["end_x"]!);
                var exportedEndY = UnitConverter.ParseDouble(targetRow["end_y"]!);

                RevitTestHelper.AssertClose(5.0, exportedStartX, 1e-4, "exported start_x");
                RevitTestHelper.AssertClose(0.0, exportedStartY, 1e-4, "exported start_y");
                RevitTestHelper.AssertClose(5.0, exportedEndX, 1e-4, "exported end_x");
                RevitTestHelper.AssertClose(20.0, exportedEndY, 1e-4, "exported end_y");

                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow]);
                await Assert.That(csvRows.Count).IsEqualTo(1);
                await Assert.That(csvRows[0]["number"]).IsEqualTo("B1");
            }
            finally
            {
                using var txClean = new Transaction(doc, "Cleanup");
                txClean.Start();
                doc.Delete(grid.Id);
                txClean.Commit();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Grid_UpdateName()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txCreate = new Transaction(doc, "Create Grid");
            txCreate.Start();
            var line = Line.CreateBound(
                new XYZ(0, 0, 0),
                new XYZ(UnitConverter.LengthToFeet(15), 0, 0));
            var grid = Grid.Create(doc, line);
            grid.Name = "OldName";
            txCreate.Commit();

            var importer = new GridImporter();
            var idMap = new IdMap();
            idMap.Register(grid.UniqueId, grid.Id);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = grid.UniqueId,
                    ["number"] = "NewName",
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "15",
                    ["end_y"] = "0",
                }
            };

            using var tx = new Transaction(doc, "Test Grid Update");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Updated).IsEqualTo(1);
            await Assert.That(grid.Name).IsEqualTo("NewName");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
