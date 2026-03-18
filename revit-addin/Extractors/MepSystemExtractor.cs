using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class MepSystemExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["system_type"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        var param = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
        if (param is not null)
        {
            var value = param.AsString();
            fields["system_type"] = MapSystemType(value);
        }

        return fields;
    }

    static string? MapSystemType(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var lower = value.ToLowerInvariant();
        return lower switch
        {
            _ when lower.Contains("supply air") || lower.Contains("return air") || lower.Contains("exhaust") || lower.Contains("hvac") => "hvac",
            _ when lower.Contains("domestic") || lower.Contains("sanitary") || lower.Contains("plumbing") || lower.Contains("vent") => "plumbing",
            _ when lower.Contains("fire") || lower.Contains("sprinkler") => "fire_protection",
            _ when lower.Contains("power") || lower.Contains("electrical") || lower.Contains("lighting") || lower.Contains("switch") => "electrical",
            _ when lower.Contains("data") || lower.Contains("communication") || lower.Contains("telephone") || lower.Contains("security") => "data_comm",
            _ when lower.Contains("gas") => "gas",
            _ => "other"
        };
    }
}
