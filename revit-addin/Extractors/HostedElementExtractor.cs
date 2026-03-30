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

            // Project the hosted element's location onto the host's curve to get the normalized parameter
            if (element.Location is LocationPoint lp && host.Location is LocationCurve hostCurve)
            {
                var result = hostCurve.Curve.Project(lp.Point);
                if (result is not null)
                {
                    var normalized = hostCurve.Curve.ComputeNormalizedParameter(result.Parameter);
                    fields["position"] = UnitConverter.FormatDouble(normalized);
                }
            }
        }

        return fields;
    }
}
