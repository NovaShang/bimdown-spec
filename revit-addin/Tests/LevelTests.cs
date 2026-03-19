using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class LevelTests : RevitApiTest
{
    [Test]
    public async Task Export_Levels_ContainsExpectedColumns()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var exporter = new LevelTableExporter();
            var rows = exporter.Export(doc);

            // Default document should have at least one level
            await Assert.That(rows.Count).IsGreaterThanOrEqualTo(1);
            await Assert.That(rows[0].ContainsKey("id")).IsTrue();
            await Assert.That(rows[0].ContainsKey("name")).IsTrue();
            await Assert.That(rows[0].ContainsKey("elevation")).IsTrue();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Export_Levels_ElevationInMeters()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var exporter = new LevelTableExporter();
            var rows = exporter.Export(doc);

            foreach (var row in rows)
            {
                var elevationStr = row["elevation"];
                await Assert.That(elevationStr).IsNotNull();

                var elevation = UnitConverter.ParseDouble(elevationStr!);
                await Assert.That(double.IsFinite(elevation)).IsTrue();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Level_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var importer = new LevelImporter();
            var idMap = new IdMap();
            importer.SetIdMap(idMap);

            // Include existing levels so DiffEngine won't try to delete them
            var csvRows = RevitTestHelper.PreserveExistingElements(
                doc, BuiltInCategory.OST_Levels,
                el => new Dictionary<string, string?>
                {
                    ["id"] = el.UniqueId,
                    ["name"] = ((Level)el).Name,
                    ["elevation"] = UnitConverter.FormatDouble(UnitConverter.Length(((Level)el).Elevation)),
                });

            csvRows.Add(new Dictionary<string, string?>
            {
                ["id"] = "test-level-001",
                ["name"] = "BimDown Test Level",
                ["number"] = "TL-1",
                ["elevation"] = "15.5",
            });

            using var tx = new Transaction(doc, "Test Level Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            // Verify the created level
            var levelId = idMap.Resolve(doc, "test-level-001");
            await Assert.That(levelId).IsNotNull();

            var level = doc.GetElement(levelId!) as Level;
            await Assert.That(level).IsNotNull();
            await Assert.That(level!.Name).IsEqualTo("BimDown Test Level");

            var expectedElevationFeet = UnitConverter.LengthToFeet(15.5);
            RevitTestHelper.AssertClose(expectedElevationFeet, level.Elevation, 1e-6, "elevation");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Level_ExportThenImport()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            // Create a level
            using var txCreate = new Transaction(doc, "Create Test Level");
            txCreate.Start();
            var level = Level.Create(doc, UnitConverter.LengthToFeet(12.0));
            level.Name = "RoundTrip Test Level";
            txCreate.Commit();

            try
            {
                // Export
                var exporter = new LevelTableExporter();
                var exportedRows = exporter.Export(doc);

                var targetRow = exportedRows.Find(r => r["name"] == "RoundTrip Test Level");
                await Assert.That(targetRow).IsNotNull();

                // CSV round-trip
                var (columns, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow!]);
                await Assert.That(csvRows.Count).IsEqualTo(1);

                // Verify exported elevation
                var exportedElevation = UnitConverter.ParseDouble(csvRows[0]["elevation"]!);
                RevitTestHelper.AssertClose(12.0, exportedElevation, 1e-4, "exported elevation");
            }
            finally
            {
                using var txClean = new Transaction(doc, "Cleanup");
                txClean.Start();
                doc.Delete(level.Id);
                txClean.Commit();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Level_UpdateExisting()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            using var tx = new Transaction(doc, "Test Level Update");
            tx.Start();
            var level = Level.Create(doc, UnitConverter.LengthToFeet(5.0));
            level.Name = "Original Name";

            var importer = new LevelImporter();
            var idMap = new IdMap();
            idMap.Register(level.UniqueId, level.Id);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = level.UniqueId,
                    ["name"] = "Updated Name",
                    ["elevation"] = "10.0",
                }
            };

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Updated).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            await Assert.That(level.Name).IsEqualTo("Updated Name");
            RevitTestHelper.AssertClose(UnitConverter.LengthToFeet(10.0), level.Elevation, 1e-6, "updated elevation");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
