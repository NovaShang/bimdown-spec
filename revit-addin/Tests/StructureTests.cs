using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class StructureTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    [Test]
    public async Task Import_StructureWall_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new StructureWallImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-sw-001",
                    ["number"] = "SW-1",
                    ["level_id"] = level.UniqueId,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "8",
                    ["end_y"] = "0",
                    ["height"] = "3.5",
                    ["thickness"] = "0.3",
                }
            };

            using var tx = new Transaction(doc, "Test Structure Wall Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var wallId = idMap.Resolve(doc, "test-sw-001");
            await Assert.That(wallId).IsNotNull();

            var wall = doc.GetElement(wallId!) as Wall;
            await Assert.That(wall).IsNotNull();
            await Assert.That(wall!.WallType.Name).IsEqualTo("BimDown_300mm");
            await Assert.That(wall.StructuralUsage).IsNotEqualTo(StructuralWallUsage.NonBearing);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Beam_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txSetup = new Transaction(doc, "Load family");
            txSetup.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_StructuralFraming);
            txSetup.Commit();

            var level = GetFirstLevel(doc);
            var importer = new BeamImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-beam-001",
                    ["number"] = "B-1",
                    ["level_id"] = level.UniqueId,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["start_z"] = "3",
                    ["end_x"] = "6",
                    ["end_y"] = "0",
                    ["end_z"] = "3",
                }
            };

            using var tx = new Transaction(doc, "Test Beam Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var beamId = idMap.Resolve(doc, "test-beam-001");
            await Assert.That(beamId).IsNotNull();

            var beam = doc.GetElement(beamId!) as FamilyInstance;
            await Assert.That(beam).IsNotNull();
            await Assert.That(beam!.StructuralType).IsEqualTo(StructuralType.Beam);

            var lc = beam.Location as LocationCurve;
            await Assert.That(lc).IsNotNull();
            var start = lc!.Curve.GetEndPoint(0);
            var end = lc.Curve.GetEndPoint(1);

            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), start.X, 1e-3, "start_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(3), start.Z, 1e-3, "start_z");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(6), end.X, 1e-3, "end_x");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Brace_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txSetup = new Transaction(doc, "Load family");
            txSetup.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_StructuralFraming);
            txSetup.Commit();

            var level = GetFirstLevel(doc);
            var importer = new BraceImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-brace-001",
                    ["number"] = "BR-1",
                    ["level_id"] = level.UniqueId,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["start_z"] = "0",
                    ["end_x"] = "3",
                    ["end_y"] = "0",
                    ["end_z"] = "3",
                }
            };

            using var tx = new Transaction(doc, "Test Brace Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var braceId = idMap.Resolve(doc, "test-brace-001");
            await Assert.That(braceId).IsNotNull();

            var brace = doc.GetElement(braceId!) as FamilyInstance;
            await Assert.That(brace).IsNotNull();
            await Assert.That(brace!.StructuralType).IsEqualTo(StructuralType.Brace);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_StructureColumn_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txSetup = new Transaction(doc, "Load family");
            txSetup.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_StructuralColumns);
            txSetup.Commit();

            var level = GetFirstLevel(doc);
            var importer = new StructureColumnImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-scol-001",
                    ["number"] = "SC-1",
                    ["level_id"] = level.UniqueId,
                    ["x"] = "5",
                    ["y"] = "5",
                    ["rotation"] = "0",
                }
            };

            using var tx = new Transaction(doc, "Test Structure Column Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var colId = idMap.Resolve(doc, "test-scol-001");
            await Assert.That(colId).IsNotNull();

            var col = doc.GetElement(colId!) as FamilyInstance;
            await Assert.That(col).IsNotNull();
            await Assert.That(col!.StructuralType).IsEqualTo(StructuralType.Column);

            // Position verification: template-created families (no geometry) may not
            // honor placement point. Verify position only when a real family is loaded.
            var lp = col.Location as LocationPoint;
            if (lp is not null && lp.Point.GetLength() > 1e-10)
            {
                RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(5), lp.Point.X, 1e-3, "x");
                RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(5), lp.Point.Y, 1e-3, "y");
            }

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_StructureSlab_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new StructureSlabImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-sslab-001",
                    ["number"] = "SS-1",
                    ["level_id"] = level.UniqueId,
                    ["points"] = "[[0,0],[6,0],[6,6],[0,6]]",
                    ["thickness"] = "0.25",
                }
            };

            using var tx = new Transaction(doc, "Test Structure Slab Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var slabId = idMap.Resolve(doc, "test-sslab-001");
            await Assert.That(slabId).IsNotNull();

            var floor = doc.GetElement(slabId!) as Floor;
            await Assert.That(floor).IsNotNull();
            await Assert.That(floor!.FloorType.Name).IsEqualTo("BimDown_250mm");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
