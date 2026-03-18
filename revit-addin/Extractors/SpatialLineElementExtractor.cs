using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

class SpatialLineElementExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["start_z", "end_z"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        if (element.Location is LocationCurve { Curve: Line line })
        {
            fields["start_z"] = UnitConverter.FormatDouble(UnitConverter.Length(line.GetEndPoint(0).Z));
            fields["end_z"] = UnitConverter.FormatDouble(UnitConverter.Length(line.GetEndPoint(1).Z));
        }

        return fields;
    }
}
