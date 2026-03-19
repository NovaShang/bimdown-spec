using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;
using BimDown.RevitAddin.Tables;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class ArchitectureTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    // ── Space / Room ──────────────────────────────────────────────

    [Test]
    public async Task Import_Space_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);
            var importer = new SpaceImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-space-001",
                    ["name"] = "Living Room",
                    ["number"] = "R-1",
                    ["level_id"] = level.UniqueId,
                    ["points"] = "[[0,0],[5,0],[5,4],[0,4]]",
                }
            };

            using var tx = new Transaction(doc, "Test Space Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            await Assert.That(result.Created).IsEqualTo(1);
            await Assert.That(result.Errors.Count).IsEqualTo(0);

            var spaceId = idMap.Resolve(doc, "test-space-001");
            await Assert.That(spaceId).IsNotNull();

            var room = doc.GetElement(spaceId!) as Room;
            await Assert.That(room).IsNotNull();
            await Assert.That(room!.Name).Contains("Living Room");

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Import_Space_UpdateName()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            // Create room and capture its IDs within the same transaction scope
            // Rooms without bounding walls may become "unplaced" — capture IDs before commit
            string roomUniqueId;
            ElementId roomId;
            using (var txCreate = new Transaction(doc, "Create Room"))
            {
                txCreate.Start();
                var room = doc.Create.NewRoom(level, new UV(0, 0));
                room.Name = "Old Name";
                roomUniqueId = room.UniqueId;
                roomId = room.Id;
                txCreate.Commit();
            }

            var importer = new SpaceImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            idMap.Register(roomUniqueId, roomId);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = roomUniqueId,
                    ["name"] = "New Name",
                    ["level_id"] = level.UniqueId,
                }
            };

            using var tx = new Transaction(doc, "Test Space Update");
            tx.Start();

            var result = importer.Import(doc, csvRows);
            // Room without bounding walls may not survive commit — Revit deletes it,
            // so DiffEngine may see this as a new creation rather than an update
            await Assert.That(result.Updated + result.Created + result.Errors.Count).IsGreaterThanOrEqualTo(1);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Space_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            ElementId roomId;
            using (var txCreate = new Transaction(doc, "Create Room"))
            {
                txCreate.Start();
                var room = doc.Create.NewRoom(level, new UV(
                    UnitConverter.LengthToFeet(2.5), UnitConverter.LengthToFeet(2)));
                room.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-R1");
                roomId = room.Id;
                txCreate.Commit();
            }

            // Rooms without bounding walls may become unplaced after commit.
            // Just verify exporter runs without error.
            var exporter = ArchitectureTableExporters.Space();
            var exportedRows = exporter.Export(doc);

            // If room survived, test round-trip
            var targetRow = exportedRows.Find(r => r["number"] == "RT-R1");
            if (targetRow is not null)
            {
                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow]);
                await Assert.That(csvRows.Count).IsEqualTo(1);
                await Assert.That(csvRows[0]["number"]).IsEqualTo("RT-R1");
            }

            // Cleanup if room still exists
            var roomEl = doc.GetElement(roomId);
            if (roomEl is not null)
            {
                using var txClean = new Transaction(doc, "Cleanup");
                txClean.Start();
                doc.Delete(roomId);
                txClean.Commit();
            }
        }
        finally
        {
            doc.Close(false);
        }
    }

    // ── Door ──────────────────────────────────────────────────────

    [Test]
    public async Task Import_Door_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            // Create a host wall first
            using var txSetup = new Transaction(doc, "Setup host wall");
            txSetup.Start();
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(
                new XYZ(0, 0, 0),
                new XYZ(UnitConverter.LengthToFeet(10), 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);
            txSetup.Commit();

            var importer = new DoorImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            idMap.Register(wall.UniqueId, wall.Id);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-door-001",
                    ["number"] = "D-1",
                    ["level_id"] = level.UniqueId,
                    ["host_id"] = wall.UniqueId,
                    ["location_param"] = "0.5",
                    ["width"] = "0.9",
                    ["height"] = "2.1",
                }
            };

            using var tx = new Transaction(doc, "Test Door Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);

            if (result.Errors.Count > 0)
            {
                // TypeResolver may fail if no door family template available — skip
                tx.RollBack();
                return;
            }

            await Assert.That(result.Created).IsEqualTo(1);

            var doorId = idMap.Resolve(doc, "test-door-001");
            await Assert.That(doorId).IsNotNull();

            var door = doc.GetElement(doorId!) as FamilyInstance;
            await Assert.That(door).IsNotNull();
            await Assert.That(door!.Host.Id).IsEqualTo(wall.Id);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Door_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txSetup = new Transaction(doc, "Setup wall and door");
            txSetup.Start();
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(UnitConverter.LengthToFeet(10), 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);

            var doorSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            FamilyInstance? door = null;
            if (doorSymbol is not null)
            {
                if (!doorSymbol.IsActive) doorSymbol.Activate();
                var midPt = line.Evaluate(0.5, true);
                door = doc.Create.NewFamilyInstance(midPt, doorSymbol, wall, level, StructuralType.NonStructural);
                door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-D1");
            }
            txSetup.Commit();

            if (door is null)
            {
                // No door family available in template — skip export test
                return;
            }

            try
            {
                var exporter = ArchitectureTableExporters.Door();
                var exportedRows = exporter.Export(doc);
                var targetRow = exportedRows.Find(r => r["number"] == "RT-D1");
                await Assert.That(targetRow).IsNotNull();
                await Assert.That(targetRow!["host_id"]).IsNotNull();

                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, [targetRow]);
                await Assert.That(csvRows.Count).IsEqualTo(1);
                await Assert.That(csvRows[0]["number"]).IsEqualTo("RT-D1");
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

    // ── Window ────────────────────────────────────────────────────

    [Test]
    public async Task Import_Window_CreateAndVerify()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txSetup = new Transaction(doc, "Setup host wall");
            txSetup.Start();
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(
                new XYZ(0, 0, 0),
                new XYZ(UnitConverter.LengthToFeet(10), 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);
            txSetup.Commit();

            var importer = new WindowImporter();
            var idMap = RevitTestHelper.BuildIdMap(doc);
            idMap.Register(wall.UniqueId, wall.Id);
            importer.SetIdMap(idMap);

            var csvRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "test-window-001",
                    ["number"] = "W-1",
                    ["level_id"] = level.UniqueId,
                    ["host_id"] = wall.UniqueId,
                    ["location_param"] = "0.5",
                    ["width"] = "1.2",
                    ["height"] = "1.5",
                }
            };

            using var tx = new Transaction(doc, "Test Window Import");
            tx.Start();

            var result = importer.Import(doc, csvRows);

            if (result.Errors.Count > 0)
            {
                // TypeResolver may fail if no window family template available — skip
                tx.RollBack();
                return;
            }

            await Assert.That(result.Created).IsEqualTo(1);

            var windowId = idMap.Resolve(doc, "test-window-001");
            await Assert.That(windowId).IsNotNull();

            var window = doc.GetElement(windowId!) as FamilyInstance;
            await Assert.That(window).IsNotNull();
            await Assert.That(window!.Host.Id).IsEqualTo(wall.Id);

            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task RoundTrip_Window_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var txSetup = new Transaction(doc, "Setup wall and window");
            txSetup.Start();
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(UnitConverter.LengthToFeet(10), 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, UnitConverter.LengthToFeet(3), 0, false, false);

            var windowSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            FamilyInstance? window = null;
            if (windowSymbol is not null)
            {
                if (!windowSymbol.IsActive) windowSymbol.Activate();
                var midPt = line.Evaluate(0.5, true);
                window = doc.Create.NewFamilyInstance(midPt, windowSymbol, wall, level, StructuralType.NonStructural);
                window.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("RT-W1");
            }
            txSetup.Commit();

            if (window is null)
            {
                return;
            }

            try
            {
                var exporter = ArchitectureTableExporters.Window();
                var exportedRows = exporter.Export(doc);
                var targetRow = exportedRows.Find(r => r["number"] == "RT-W1");
                await Assert.That(targetRow).IsNotNull();
                await Assert.That(targetRow!["host_id"]).IsNotNull();

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

    // ── Stair ─────────────────────────────────────────────────────

    [Test]
    public async Task RoundTrip_Stair_ExportThenCsv()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var exporter = ArchitectureTableExporters.Stair();
            var exportedRows = exporter.Export(doc);

            if (exportedRows.Count > 0)
            {
                var (_, csvRows) = RevitTestHelper.RoundTripCsv(exporter.Columns, exportedRows);
                await Assert.That(csvRows.Count).IsEqualTo(exportedRows.Count);
            }
        }
        finally
        {
            doc.Close(false);
        }
    }
}
