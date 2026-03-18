using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class PolygonElementExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["points", "area"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        var polygon = GeometryUtils.GetTopFacePolygon(element);
        if (polygon is not null)
        {
            fields["points"] = GeometryUtils.SerializePolygon(polygon);
        }

        var area = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble();
        fields["area"] = area is { } a ? UnitConverter.FormatDouble(UnitConverter.Area(a)) : null;

        return fields;
    }
}
