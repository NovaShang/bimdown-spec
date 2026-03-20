using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class SectionProfileExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["shape", "size_x", "size_y"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        // Try round (diameter)
        var diameter = element.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.AsDouble()
                    ?? ParameterUtils.FindDoubleParameterByNames(element, "diameter", "d", "直径");
        if (diameter is { } d && d > 0)
        {
            fields["shape"] = "round";
            fields["size_x"] = UnitConverter.FormatDouble(UnitConverter.Length(d));
            fields["size_y"] = UnitConverter.FormatDouble(UnitConverter.Length(d));
            return fields;
        }

        // Try rectangular (width/height)
        var width = element.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.AsDouble()
                 ?? ParameterUtils.FindDoubleParameterByNames(element, "width", "w", "b", "宽");
        var height = element.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.AsDouble()
                  ?? ParameterUtils.FindDoubleParameterByNames(element, "height", "depth", "h", "d", "高", "深");
        if (width is { } w && height is { } h)
        {
            fields["shape"] = "rect";
            fields["size_x"] = UnitConverter.FormatDouble(UnitConverter.Length(w));
            fields["size_y"] = UnitConverter.FormatDouble(UnitConverter.Length(h));
            return fields;
        }

        return fields;
    }
}
