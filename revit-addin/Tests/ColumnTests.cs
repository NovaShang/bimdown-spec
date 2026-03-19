using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class ColumnTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    [Test]
    public async Task Import_Column_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var txSetup = new Transaction(doc, "Load family");
            txSetup.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_Columns);
            txSetup.Commit();

            var level = GetFirstLevel(doc);
            var importer = new ColumnImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-col-001",
                    ["name"] = "Test Column",
                    ["number"] = "C-1",
                    ["level_id"] = level.UniqueId,
                    ["x"] = "3",
                    ["y"] = "4",
                    ["rotation"] = "45",
                }
            };

            using var tx = new Transaction(doc, "Test Column Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var colId = idMap.Resolve(doc, "test-col-001");
            await Assert.That(colId).IsNotNull();

            var column = doc.GetElement(colId!) as FamilyInstance;
            await Assert.That(column).IsNotNull();

            var lp = column!.Location as LocationPoint;
            await Assert.That(lp).IsNotNull();
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(3), lp!.Point.X, 1e-3, "x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(4), lp.Point.Y, 1e-3, "y");

            var expectedRadians = UnitConverter.AngleToRadians(45);
            RevitTestHelper.AssertClose(expectedRadians, lp.Rotation, 1e-3, "rotation");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Column_UpdatePosition()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            // Create column in a committed transaction so position is fully established
            using (var txSetup = new Transaction(doc, "Setup Column"))
            {
                txSetup.Start();
                RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_Columns);
                var symbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Columns)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .First();
                if (!symbol.IsActive) symbol.Activate();

                var pt = new XYZ(UnitConverter.LengthToFeet(1), UnitConverter.LengthToFeet(1), level.Elevation);
                doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural);
                txSetup.Commit();
            }

            var column = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Columns)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .First();

            var importer = new ColumnImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            idMap.Register(column.UniqueId, column.Id);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = column.UniqueId,
                    ["level_id"] = level.UniqueId,
                    ["x"] = "5",
                    ["y"] = "7",
                    ["rotation"] = "0",
                }
            };

            using var tx = new Transaction(doc, "Test Column Update");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Updated).IsEqualTo(1);

            var lp = column.Location as LocationPoint;
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(5), lp!.Point.X, 1e-3, "updated x");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(7), lp.Point.Y, 1e-3, "updated y");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Column_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txCreate = new Transaction(doc, "Create Test Column");
            txCreate.Start();
            RevitTestHelper.EnsureFamilyLoaded(doc, Application, BuiltInCategory.OST_Columns);
            var symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Columns)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .First();
            if (!symbol.IsActive) symbol.Activate();

            var pt = new XYZ(UnitConverter.LengthToFeet(10), UnitConverter.LengthToFeet(20), level.Elevation);
            var column = doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural);
            column.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-C1");
            txCreate.Commit();

            try
            {
                var exporter = ArchitectureTableExporters.Column();
                var exportedRows = exporter.Export(doc);
                var targetRow = exportedRows.Find(r => r["number"] == "RT-C1");
                await Assert.That(targetRow).IsNotNull();

                var x = UnitConverter.ParseDouble(targetRow!["x"]!);
                var y = UnitConverter.ParseDouble(targetRow["y"]!);
                RevitTestHelper.AssertClose(10.0, x, 1e-4, "exported x");
                RevitTestHelper.AssertClose(20.0, y, 1e-4, "exported y");

                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow]);
                await Assert.That(csvRows[0]["number"]).IsEqualTo("RT-C1");
                await Assert.That(csvRows[0]["x"]).IsEqualTo(targetRow["x"]);
                await Assert.That(csvRows[0]["y"]).IsEqualTo(targetRow["y"]);
            }
            finally
            {
                using var txClean = new Transaction(doc, "Cleanup");
                txClean.Start();
                doc.Delete(column.Id);
                txClean.Commit();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }
}
