using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class HostedElementExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["host_id", "position"];
    public IReadOnlyList<string> ComputedFieldNames { get; } = [];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        if (element is FamilyInstance fi && fi.Host is { } host)
        {
            fields["host_id"] = host.UniqueId;

            var point = GetPoint(element);
            var curve = GetHostCurve(host);
            if (point is not null && curve is not null)
            {
                var result = curve.Project(point);
                if (result is not null)
                {
                    var normalized = curve.ComputeNormalizedParameter(result.Parameter);
                    var distanceMeters = UnitConverter.Length(normalized * curve.Length);
                    fields["position"] = UnitConverter.FormatDouble(distanceMeters);
                }
            }
        }

        return fields;
    }

    static XYZ? GetPoint(Element element)
    {
        if (element.Location is LocationPoint lp)
            return lp.Point;

        // Fallback: bounding box center
        var bb = element.get_BoundingBox(null);
        if (bb is not null)
            return (bb.Min + bb.Max) / 2;

        return null;
    }

    static Curve? GetHostCurve(Element host)
    {
        if (host.Location is LocationCurve lc)
            return lc.Curve;

        // Curtain wall panel: walk up to the parent wall
        if (host is Autodesk.Revit.DB.Panel panel)
        {
            var wall = panel.Document.GetElement(panel.FindHostPanel()) as Wall;
            if (wall?.Location is LocationCurve wallCurve)
                return wallCurve.Curve;
        }

        return null;
    }
}
