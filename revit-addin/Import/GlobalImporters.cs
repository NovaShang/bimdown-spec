using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

class LevelImporter() : TableImporterBase("level", 0, [BuiltInCategory.OST_Levels])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        var elevationStr = row.GetValueOrDefault("elevation")
            ?? throw new InvalidOperationException("elevation is required");
        var elevationFeet = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(elevationStr));

        var level = Level.Create(doc, elevationFeet);

        var name = row.GetValueOrDefault("name");
        if (name is not null) level.Name = name;
        SetMark(level, row);

        return level;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        if (element is not Level level) return;

        var name = row.GetValueOrDefault("name");
        if (name is not null && level.Name != name) level.Name = name;

        var elevationStr = row.GetValueOrDefault("elevation");
        if (elevationStr is not null)
            level.Elevation = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(elevationStr));

        SetMark(level, row);
    }
}

class GridImporter() : TableImporterBase("grid", 1, [BuiltInCategory.OST_Grids])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        var line = ParseGridLine(row);
        var grid = Grid.Create(doc, line);

        var number = row.GetValueOrDefault("number");
        if (number is not null) grid.Name = number;

        return grid;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        if (element is not Grid grid) return;

        var number = row.GetValueOrDefault("number");
        if (number is not null && grid.Name != number) grid.Name = number;

        // Grid geometry updates are limited in Revit — log a warning if endpoints differ
    }

    static Line ParseGridLine(Dictionary<string, string?> row)
    {
        var sx = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["start_x"]!));
        var sy = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["start_y"]!));
        var ex = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["end_x"]!));
        var ey = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["end_y"]!));
        return Line.CreateBound(new XYZ(sx, sy, 0), new XYZ(ex, ey, 0));
    }
}
