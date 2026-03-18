using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

class LineElementExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["start_x", "start_y", "end_x", "end_y", "length"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        if (element.Location is LocationCurve { Curve: Line line })
        {
            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            fields["start_x"] = UnitConverter.FormatDouble(UnitConverter.Length(start.X));
            fields["start_y"] = UnitConverter.FormatDouble(UnitConverter.Length(start.Y));
            fields["end_x"] = UnitConverter.FormatDouble(UnitConverter.Length(end.X));
            fields["end_y"] = UnitConverter.FormatDouble(UnitConverter.Length(end.Y));
            fields["length"] = UnitConverter.FormatDouble(UnitConverter.Length(line.Length));
        }

        return fields;
    }
}
