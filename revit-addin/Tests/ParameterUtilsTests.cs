using Autodesk.Revit.DB;
using BimDown.RevitAddin.Extractors;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class ParameterUtilsTests : RevitApiTest
{
    static Level GetFirstLevel(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .First();

    [Test]
    public async Task FindParameterByNames_ReturnsExpectedValue()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var tx = new Transaction(doc, "Test Param");
            tx.Start();
            
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, 10, 0, false, false);
            
            // "Base Offset" is a built-in double parameter.
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(5.0);
            
            // "Mark" is a built-in string parameter
            wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("TestMark123");
            
            tx.Commit();

            var markValue = BimDown.RevitAddin.Extractors.ParameterUtils.FindStringParameterByNames(wall, "Mark", "ALL_MODEL_MARK", "标记");
            await Assert.That(markValue).IsEqualTo("TestMark123");

            var offsetValue = BimDown.RevitAddin.Extractors.ParameterUtils.FindDoubleParameterByNames(wall, "Base Offset", "底部偏移");
            await Assert.That(offsetValue).IsEqualTo(5.0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task ElementExtractor_ExtractsRoomNumberAndWallOffset()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var level = GetFirstLevel(doc);

            using var tx = new Transaction(doc, "Test ElementExtractor");
            tx.Start();
            
            // Test Room
            var room = doc.Create.NewRoom(level, new UV(0, 0));
            room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.Set("ROOM-456");
            
            // Test Wall Location
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .First(wt => wt.Kind == WallKind.Basic);
            var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0));
            var wall = Wall.Create(doc, line, wallType.Id, level.Id, 10, 0, false, false);
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(7.5);
            
            var extractor = new ElementExtractor();
            
            var roomFields = extractor.Extract(room);
            await Assert.That(roomFields["number"]).IsEqualTo("ROOM-456");
            
            var wallFields = extractor.Extract(wall);
            // 7.5 feet -> formatted double using UnitConverter
            var expectedOffsetStr = BimDown.RevitAddin.UnitConverter.FormatDouble(BimDown.RevitAddin.UnitConverter.Length(7.5));
            await Assert.That(wallFields["base_offset"]).IsEqualTo(expectedOffsetStr);
            
            tx.RollBack();
        }
        finally
        {
            doc.Close(false);
        }
    }
}
