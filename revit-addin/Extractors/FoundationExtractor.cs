using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

/// <summary>
/// Unified extractor for all foundation geometry modes (isolated/strip/raft).
/// Detects element type and extracts the appropriate fields.
/// </summary>
public class FoundationExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } =
        ["thickness", "width", "length", "x", "y", "rotation", "start_x", "start_y", "end_x", "end_y", "points", "area"];

    public IReadOnlyList<string> ComputedFieldNames { get; } =
        ["x", "y", "rotation", "start_x", "start_y", "end_x", "end_y", "points", "area"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        // Thickness (common to all foundation types)
        var thickness = element.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble()
                     ?? element.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
        fields["thickness"] = thickness is { } tv ? UnitConverter.FormatDouble(UnitConverter.Length(tv)) : null;

        // Width (isolated and strip)
        var width = element.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)?.AsDouble()
                 ?? element.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();
        fields["width"] = width is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;

        if (element is Floor)
        {
            // Raft foundation → polygon
            var polygon = GeometryUtils.GetTopFacePolygon(element);
            if (polygon is not null)
                fields["points"] = GeometryUtils.SerializePolygon(polygon);

            var area = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble();
            fields["area"] = area is { } a ? UnitConverter.FormatDouble(UnitConverter.Area(a)) : null;
        }
        else if (element.Location is LocationCurve { Curve: Line line })
        {
            // Strip foundation → line
            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            fields["start_x"] = UnitConverter.FormatDouble(UnitConverter.Length(start.X));
            fields["start_y"] = UnitConverter.FormatDouble(UnitConverter.Length(start.Y));
            fields["end_x"] = UnitConverter.FormatDouble(UnitConverter.Length(end.X));
            fields["end_y"] = UnitConverter.FormatDouble(UnitConverter.Length(end.Y));
        }
        else if (element.Location is LocationPoint lp)
        {
            // Isolated foundation → point
            fields["x"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.X));
            fields["y"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.Y));
            fields["rotation"] = UnitConverter.FormatDouble(UnitConverter.Angle(lp.Rotation));

            var length = element.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH)?.AsDouble();
            fields["length"] = length is { } lv ? UnitConverter.FormatDouble(UnitConverter.Length(lv)) : null;
        }

        return fields;
    }
}
