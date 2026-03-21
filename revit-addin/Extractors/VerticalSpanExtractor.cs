using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class VerticalSpanExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["top_level_id", "top_offset", "height"];
    public IReadOnlyList<string> ComputedFieldNames { get; } = ["height"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        // Top level
        var topLevelId = element.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.AsElementId()
                      ?? element.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.AsElementId()
                      ?? element.get_Parameter(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM)?.AsElementId();
        if (topLevelId is not null && topLevelId != ElementId.InvalidElementId)
        {
            fields["top_level_id"] = element.Document.GetElement(topLevelId)?.UniqueId;
        }

        // Top offset
        var topOffset = element.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble()
                     ?? element.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.AsDouble()
                     ?? element.get_Parameter(BuiltInParameter.STAIRS_TOP_OFFSET)?.AsDouble();
        fields["top_offset"] = topOffset is { } to ? UnitConverter.FormatDouble(UnitConverter.Length(to)) : null;

        // Height
        var height = element.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble()
                  ?? element.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
        fields["height"] = height is { } h ? UnitConverter.FormatDouble(UnitConverter.Length(h)) : null;

        return fields;
    }
}
