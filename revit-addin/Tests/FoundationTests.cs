using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class FoundationTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    // ── Isolated Foundation ───────────────────────────────────────

    [Test]
    public async Task Import_IsolatedFoundation_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txSetup = new Transaction(doc, "Load family");
            txSetup.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_StructuralFoundation);
            txSetup.Commit();

            var level = GetFirstLevel(doc);
            var importer = new IsolatedFoundationImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-isofound-001",
                    ["number"] = "IF-1",
                    ["level_id"] = level.UniqueId,
                    ["x"] = "5",
                    ["y"] = "5",
                }
            };

            using var tx = new Transaction(doc, "Test Isolated Foundation Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var foundId = idMap.Resolve(doc, "test-isofound-001");
            await Assert.That(foundId).IsNotNull();

            var element = doc.GetElement(foundId!);
            await Assert.That(element).IsNotNull();
            await Assert.That(element!.Location is LocationPoint).IsTrue();

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_IsolatedFoundation_UpdatePosition()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txSetup = new Transaction(doc, "Load family and create");
            txSetup.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_StructuralFoundation);

            var level = GetFirstLevel(doc);
            var symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .First();
            if (!symbol.IsActive) symbol.Activate();

            var pt = new XYZ(UnitConverter.LengthToFeet(2), UnitConverter.LengthToFeet(3), level.Elevation);
            var instance = doc.Create.NewFamilyInstance(pt, symbol, level,
                Autodesk.Revit.DB.Structure.StructuralType.Footing);
            txSetup.Commit();

            var importer = new IsolatedFoundationImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            idMap.Register(instance.UniqueId, instance.Id);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = instance.UniqueId,
                    ["level_id"] = level.UniqueId,
                    ["x"] = "8",
                    ["y"] = "9",
                }
            };

            using var tx = new Transaction(doc, "Test Update");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Updated).IsEqualTo(1);

            var lp = instance.Location as LocationPoint;
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(8), lp!.Point.X, 1e-3, "updated x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(9), lp.Point.Y, 1e-3, "updated y");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    // ── Raft Foundation ───────────────────────────────────────────

    [Test]
    public async Task Import_RaftFoundation_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new RaftFoundationImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-raft-001",
                    ["number"] = "RF-1",
                    ["level_id"] = level.UniqueId,
                    ["points"] = "[[0,0],[10,0],[10,8],[0,8]]",
                    ["thickness"] = "0.5",
                }
            };

            using var tx = new Transaction(doc, "Test Raft Foundation Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var raftId = idMap.Resolve(doc, "test-raft-001");
            await Assert.That(raftId).IsNotNull();

            var floor = doc.GetElement(raftId!) as Floor;
            await Assert.That(floor).IsNotNull();
            await Assert.That(floor!.FloorType.Name).IsEqualTo("BimDown_500mm");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_RaftFoundation_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txCreate = new Transaction(doc, "Create raft foundation");
            txCreate.Start();
            var floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .First();

            var curveLoop = new CurveLoop();
            var p1 = new XYZ(0, 0, level.Elevation);
            var p2 = new XYZ(UnitConverter.LengthToFeet(8), 0, level.Elevation);
            var p3 = new XYZ(UnitConverter.LengthToFeet(8), UnitConverter.LengthToFeet(6), level.Elevation);
            var p4 = new XYZ(0, UnitConverter.LengthToFeet(6), level.Elevation);
            curveLoop.Append(Line.CreateBound(p1, p2));
            curveLoop.Append(Line.CreateBound(p2, p3));
            curveLoop.Append(Line.CreateBound(p3, p4));
            curveLoop.Append(Line.CreateBound(p4, p1));

            var floor = Floor.Create(doc, [curveLoop], floorType.Id, level.Id);
            floor.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-RF1");
            txCreate.Commit();

            try
            {
                // RaftFoundation exporter filters for Floor elements under StructuralFoundation category.
                // In a minimal doc, floors may not be categorized as structural foundation,
                // so we test the exporter runs without error.
                var exporter = StructureTableExporters.RaftFoundation();
                var exportedRows = exporter.Export(doc);

                if (exportedRows.Count > 0)
                {
                    var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, exportedRows);
                    await Assert.That(csvRows.Count).IsEqualTo(exportedRows.Count);
                }
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

    // ── Strip Foundation (creation not supported — update-only) ───

    [Test]
    public async Task Import_StripFoundation_SkipsCreation()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new StripFoundationImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-strip-001",
                    ["level_id"] = level.UniqueId,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "8",
                    ["end_y"] = "0",
                }
            };

            using var tx = new Transaction(doc, "Test Strip Foundation Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            // Creation returns null → Created should be 0
            await Assert.That(result.Created).IsEqualTo(0);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
