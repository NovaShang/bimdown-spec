using Autodesk.Revit.DB;
using static BimDown.RevitAddin.Extractors.ParameterUtils;

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
        var thickness = element.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsPositiveDouble()
                     ?? element.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM).AsPositiveDouble()
                     ?? FindDoubleParameterByNames(element, "thickness", "depth", "d", "厚", "深");
        fields["thickness"] = thickness is { } tv ? UnitConverter.FormatDouble(UnitConverter.Length(tv)) : null;

        // Width
        var width = element.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH).AsPositiveDouble()
                 ?? element.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM).AsPositiveDouble()
                 ?? FindDoubleParameterByNames(element, "width", "w", "b", "宽");
        fields["width"] = width is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;

        if (element is Floor)
        {
            // Raft foundation → polygon
            var polygon = GeometryUtils.GetTopFacePolygon(element);
            if (polygon is not null)
            {
                fields["points"] = GeometryUtils.SerializePolygon(polygon.Value.Points);
                if (polygon.Value.HasCurvedEdges)
                    fields["_has_curved_edges"] = "true";
            }

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

            var length = element.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH).AsPositiveDouble()
                      ?? FindDoubleParameterByNames(element, "length", "l", "a", "长");
            fields["length"] = length is { } lv ? UnitConverter.FormatDouble(UnitConverter.Length(lv)) : null;

            // If width/length still null, try bounding box
            if (fields["width"] is null || fields["length"] is null)
            {
                var bb = element.get_BoundingBox(null);
                if (bb is not null)
                {
                    var dx = UnitConverter.Length(bb.Max.X - bb.Min.X);
                    var dy = UnitConverter.Length(bb.Max.Y - bb.Min.Y);
                    fields["width"] ??= UnitConverter.FormatDouble(Math.Min(dx, dy));
                    fields["length"] ??= UnitConverter.FormatDouble(Math.Max(dx, dy));
                }
            }
        }

        return fields;
    }
}
