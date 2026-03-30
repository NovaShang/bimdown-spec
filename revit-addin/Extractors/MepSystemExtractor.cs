using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class MepSystemExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["system_type"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        var param = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
        fields["system_type"] = param?.AsString();

        return fields;
    }
}
