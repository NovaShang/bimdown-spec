using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Tables;

public class LevelTableExporter : ITableExporter
{
    public string TableName => "level";
    public IReadOnlyList<string> Columns { get; } = ["id", "number", "name", "elevation"];

    public List<Dictionary<string, string?>> Export(Document doc)
    {
        var rows = new List<Dictionary<string, string?>>();
        var collector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType();

        foreach (var element in collector)
        {
            if (element is not Level level) continue;
            rows.Add(new Dictionary<string, string?>
            {
                ["id"] = level.UniqueId,
                ["number"] = level.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                ["name"] = level.Name,
                ["elevation"] = UnitConverter.FormatDouble(UnitConverter.Length(level.Elevation)),
            });
        }
        return rows;
    }
}

public class GridTableExporter : ITableExporter
{
    public string TableName => "grid";
    public IReadOnlyList<string> Columns { get; } = ["id", "number", "start_x", "start_y", "end_x", "end_y"];

    public List<Dictionary<string, string?>> Export(Document doc)
    {
        var rows = new List<Dictionary<string, string?>>();
        var collector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Grids)
            .WhereElementIsNotElementType();

        foreach (var element in collector)
        {
            if (element is not Grid grid) continue;
            var curve = grid.Curve;
            var start = curve.GetEndPoint(0);
            var end = curve.GetEndPoint(1);
            rows.Add(new Dictionary<string, string?>
            {
                ["id"] = grid.UniqueId,
                ["number"] = grid.Name,
                ["start_x"] = UnitConverter.FormatDouble(UnitConverter.Length(start.X)),
                ["start_y"] = UnitConverter.FormatDouble(UnitConverter.Length(start.Y)),
                ["end_x"] = UnitConverter.FormatDouble(UnitConverter.Length(end.X)),
                ["end_y"] = UnitConverter.FormatDouble(UnitConverter.Length(end.Y)),
            });
        }
        return rows;
    }
}
