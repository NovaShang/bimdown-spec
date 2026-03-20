using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class WallTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    [Test]
    public async Task Import_Wall_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new WallImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var levelId = BimDownParameter.Get(level)!;
            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "w-1",
                    ["name"] = "Test Wall",
                    ["number"] = "W-1",
                    ["level_id"] = levelId,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "5",
                    ["end_y"] = "0",
                    ["height"] = "3",
                    ["thickness"] = "0.2",
                }
            };

            using var tx = new Transaction(doc, "Test Wall Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var wallId = idMap.Resolve(doc, "w-1");
            await Assert.That(wallId).IsNotNull();

            var wall = doc.GetElement(wallId!) as Wall;
            await Assert.That(wall).IsNotNull();

            // Verify location curve
            var lc = wall!.Location as LocationCurve;
            await Assert.That(lc).IsNotNull();
            var start = lc!.Curve.GetEndPoint(0);
            var end = lc.Curve.GetEndPoint(1);

            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), start.X, 1e-3, "start_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), start.Y, 1e-3, "start_y");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(5), end.X, 1e-3, "end_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), end.Y, 1e-3, "end_y");

            await Assert.That(wall.WallType.Name).IsEqualTo("BimDown_200mm");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Wall_ExportThenImportVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txCreate = new Transaction(doc, "Create Test Wall");
            txCreate.Start();
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(
                new XYZ(UnitConverter.LengthToFeet(1), UnitConverter.LengthToFeet(2), 0),
                new XYZ(UnitConverter.LengthToFeet(6), UnitConverter.LengthToFeet(2), 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);
            wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-W1");
            txCreate.Commit();

            try
            {
                var exporter = ArchitectureTableExporters.Wall();
                var exportedRows = exporter.Export(doc);
                var targetRow = exportedRows.Find(r => r["number"] == "RT-W1");
                await Assert.That(targetRow).IsNotNull();

                var sx = UnitConverter.ParseDouble(targetRow!["start_x"]!);
                var sy = UnitConverter.ParseDouble(targetRow["start_y"]!);
                var ex = UnitConverter.ParseDouble(targetRow["end_x"]!);
                var ey = UnitConverter.ParseDouble(targetRow["end_y"]!);

                RevitTestHelper.AssertClose(1.0, sx, 1e-4, "exported start_x");
                RevitTestHelper.AssertClose(2.0, sy, 1e-4, "exported start_y");
                RevitTestHelper.AssertClose(6.0, ex, 1e-4, "exported end_x");
                RevitTestHelper.AssertClose(2.0, ey, 1e-4, "exported end_y");

                var height = UnitConverter.ParseDouble(targetRow["height"]!);
                RevitTestHelper.AssertClose(3.0, height, 1e-4, "exported height");

                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow]);
                await Assert.That(csvRows.Count).IsEqualTo(1);
                await Assert.That(csvRows[0]["number"]).IsEqualTo("RT-W1");
            }
            finally
            {
                using var txClean = new Transaction(doc, "Cleanup");
                txClean.Start();
                doc.Delete(wall.Id);
                txClean.Commit();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Wall_UpdateGeometry()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var idMap = RevitTestHelper.BuildIdMap(doc);
            var levelId = BimDownParameter.Get(level)!;

            using var tx = new Transaction(doc, "Test Wall Update");
            tx.Start();

            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(
                new XYZ(0, 0, 0),
                new XYZ(UnitConverter.LengthToFeet(10), 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);

            BimDownParameter.Set(wall, "w-1");
            idMap.Register("w-1", wall.Id);

            var importer = new WallImporter();
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "w-1",
                    ["level_id"] = levelId,
                    ["start_x"] = "2",
                    ["start_y"] = "3",
                    ["end_x"] = "8",
                    ["end_y"] = "3",
                    ["height"] = "4",
                }
            };

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Updated).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var lc = wall.Location as LocationCurve;
            await Assert.That(lc).IsNotNull();
            var start = lc!.Curve.GetEndPoint(0);
            var end = lc.Curve.GetEndPoint(1);

            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(2), start.X, 1e-3, "updated start_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(3), start.Y, 1e-3, "updated start_y");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(8), end.X, 1e-3, "updated end_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(3), end.Y, 1e-3, "updated end_y");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Wall_DeleteUnmatched()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var idMap = RevitTestHelper.BuildIdMap(doc);
            var levelId = BimDownParameter.Get(level)!;

            using var tx = new Transaction(doc, "Test Wall Delete");
            tx.Start();

            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line1 = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(UnitConverter.LengthToFeet(5), 0, 0));
            var line2 = Line.CreateBound(new XYZ(0, UnitConverter.LengthToFeet(5), 0), new XYZ(UnitConverter.LengthToFeet(5), UnitConverter.LengthToFeet(5), 0));
            var wall1 = Wall.Create(doc, line1, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);
            var wall2 = Wall.Create(doc, line2, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);

            BimDownParameter.Set(wall1, "w-1");
            BimDownParameter.Set(wall2, "w-2");
            idMap.Register("w-1", wall1.Id);
            idMap.Register("w-2", wall2.Id);

            var importer = new WallImporter();
            importer.SetIdMap(idMap);

            // Import with only wall1 — wall2 should be deleted
            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "w-1",
                    ["level_id"] = levelId,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "5",
                    ["end_y"] = "0",
                    ["height"] = "3",
                }
            };

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Deleted).IsGreaterThanOrEqualTo(1);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
