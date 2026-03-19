using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class SlabTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    [Test]
    public async Task Import_Slab_CreateFromPolygon()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new SlabImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-slab-001",
                    ["number"] = "S-1",
                    ["level_id"] = level.UniqueId,
                    ["points"] = "[[0,0],[4,0],[4,4],[0,4]]",
                    ["function"] = "floor",
                    ["thickness"] = "0.2",
                }
            };

            using var tx = new Transaction(doc, "Test Slab Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var slabId = idMap.Resolve(doc, "test-slab-001");
            await Assert.That(slabId).IsNotNull();

            var floor = doc.GetElement(slabId!) as Floor;
            await Assert.That(floor).IsNotNull();
            await Assert.That(floor!.FloorType.Name).IsEqualTo("BimDown_200mm");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Slab_TrianglePolygon()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new SlabImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-slab-tri",
                    ["level_id"] = level.UniqueId,
                    ["points"] = "[[0,0],[6,0],[3,5]]",
                    ["function"] = "floor",
                }
            };

            using var tx = new Transaction(doc, "Test Triangle Slab");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Slab_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txCreate = new Transaction(doc, "Create Test Slab");
            txCreate.Start();

            var floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .First();

            var curveLoop = new CurveLoop();
            var p1 = new XYZ(0, 0, level.Elevation);
            var p2 = new XYZ(UnitConverter.LengthToFeet(5), 0, level.Elevation);
            var p3 = new XYZ(UnitConverter.LengthToFeet(5), UnitConverter.LengthToFeet(5), level.Elevation);
            var p4 = new XYZ(0, UnitConverter.LengthToFeet(5), level.Elevation);
            curveLoop.Append(Line.CreateBound(p1, p2));
            curveLoop.Append(Line.CreateBound(p2, p3));
            curveLoop.Append(Line.CreateBound(p3, p4));
            curveLoop.Append(Line.CreateBound(p4, p1));

            var floor = Floor.Create(doc, [curveLoop], floorType.Id, level.Id);
            floor.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-S1");
            txCreate.Commit();

            try
            {
                var exporter = ArchitectureTableExporters.Slab();
                var exportedRows = exporter.Export(doc);
                var targetRow = exportedRows.Find(r => r["number"] == "RT-S1");
                await Assert.That(targetRow).IsNotNull();

                await Assert.That(targetRow!["points"]).IsNotNull();
                await Assert.That(targetRow["function"]).IsEqualTo("floor");

                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow]);
                await Assert.That(csvRows.Count).IsEqualTo(1);
                await Assert.That(csvRows[0]["points"]).IsEqualTo(targetRow["points"]);
            }
            finally
            {
                using var txClean = new Transaction(doc, "Cleanup");
                txClean.Start();
                doc.Delete(floor.Id);
                txClean.Commit();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }
}
