using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class MepTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    [Test]
    public async Task Import_Duct_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new DuctImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-duct-001",
                    ["number"] = "D-1",
                    ["level_id"] = BimDownParameter.Get(level)!,
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["start_z"] = "3",
                    ["end_x"] = "10",
                    ["end_y"] = "0",
                    ["end_z"] = "3",
                    ["shape"] = "round",
                    ["size_x"] = "0.3",
                }
            };

            using var tx = new Transaction(doc, "Test Duct Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var ductId = idMap.Resolve(doc, "test-duct-001");
            await Assert.That(ductId).IsNotNull();

            var duct = doc.GetElement(ductId!);
            await Assert.That(duct).IsNotNull();

            var lc = duct!.Location as LocationCurve;
            await Assert.That(lc).IsNotNull();
            var start = lc!.Curve.GetEndPoint(0);
            var end = lc.Curve.GetEndPoint(1);

            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(0), start.X, 1e-3, "start_x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(3), start.Z, 1e-3, "start_z");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(10), end.X, 1e-3, "end_x");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Pipe_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new PipeImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-pipe-001",
                    ["number"] = "P-1",
                    ["level_id"] = BimDownParameter.Get(level)!,
                    ["start_x"] = "0",
                    ["start_y"] = "5",
                    ["start_z"] = "2",
                    ["end_x"] = "8",
                    ["end_y"] = "5",
                    ["end_z"] = "2",
                    ["size_x"] = "0.05",
                }
            };

            using var tx = new Transaction(doc, "Test Pipe Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var pipeId = idMap.Resolve(doc, "test-pipe-001");
            await Assert.That(pipeId).IsNotNull();

            var pipe = doc.GetElement(pipeId!);
            await Assert.That(pipe).IsNotNull();

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
