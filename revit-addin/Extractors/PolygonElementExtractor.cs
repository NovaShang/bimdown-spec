using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class PolygonElementExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["points", "area", "_has_curved_edges"];
    public IReadOnlyList<string> ComputedFieldNames { get; } = ["points", "area", "_has_curved_edges"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        var result = GeometryUtils.GetTopFacePolygon(element);
        if (result is not null)
        {
            fields["points"] = GeometryUtils.SerializePolygon(result.Value.Points);
            if (result.Value.HasCurvedEdges)
                fields["_has_curved_edges"] = "true";
        }

        var area = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble();
        fields["area"] = area is { } a ? UnitConverter.FormatDouble(UnitConverter.Area(a)) : null;

        return fields;
    }
}
