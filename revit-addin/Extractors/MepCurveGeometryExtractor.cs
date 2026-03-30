using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

/// <summary>
/// Extracts MEP curve geometry from connector positions rather than LocationCurve endpoints.
/// Connector positions align with the fitting connectors on each end, ensuring that
/// mep_curve endpoints and mep_node positions naturally coincide.
/// Falls back to LocationCurve if connectors are unavailable.
/// </summary>
public class MepCurveGeometryExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["start_x", "start_y", "end_x", "end_y", "length", "start_z", "end_z"];
    public IReadOnlyList<string> ComputedFieldNames { get; } = ["start_x", "start_y", "end_x", "end_y", "length"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        XYZ? startPt = null;
        XYZ? endPt = null;

        // Try to get endpoints from connectors (preferred for MEP curves)
        if (element is MEPCurve mepCurve)
        {
            var connectors = mepCurve.ConnectorManager?.Connectors;
            if (connectors is not null)
            {
                foreach (Connector conn in connectors)
                {
                    if (conn.ConnectorType != ConnectorType.End) continue;

                    if (startPt is null)
                        startPt = conn.Origin;
                    else if (endPt is null)
                        endPt = conn.Origin;
                }
            }
        }

        // Fallback to LocationCurve
        if ((startPt is null || endPt is null) && element.Location is LocationCurve { Curve: Line line })
        {
            startPt ??= line.GetEndPoint(0);
            endPt ??= line.GetEndPoint(1);
        }

        if (startPt is null || endPt is null) return fields;

        fields["start_x"] = UnitConverter.FormatDouble(UnitConverter.Length(startPt.X));
        fields["start_y"] = UnitConverter.FormatDouble(UnitConverter.Length(startPt.Y));
        fields["end_x"] = UnitConverter.FormatDouble(UnitConverter.Length(endPt.X));
        fields["end_y"] = UnitConverter.FormatDouble(UnitConverter.Length(endPt.Y));
        fields["start_z"] = UnitConverter.FormatDouble(UnitConverter.Length(startPt.Z));
        fields["end_z"] = UnitConverter.FormatDouble(UnitConverter.Length(endPt.Z));

        var dx = endPt.X - startPt.X;
        var dy = endPt.Y - startPt.Y;
        var dz = endPt.Z - startPt.Z;
        fields["length"] = UnitConverter.FormatDouble(UnitConverter.Length(
            Math.Sqrt(dx * dx + dy * dy + dz * dz)));

        return fields;
    }
}
