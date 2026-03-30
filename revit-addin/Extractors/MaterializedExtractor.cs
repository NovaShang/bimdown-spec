using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class MaterializedExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["material"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var materialIds = element.GetMaterialIds(false);
        string? materialName = null;
        foreach (var id in materialIds)
        {
            if (element.Document.GetElement(id) is Material mat)
            {
                materialName = mat.Name;
                break;
            }
        }
        return new Dictionary<string, string?> { ["material"] = MapToEnum(materialName) };
    }

    internal static string MapToEnum(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "composite";

        var lower = rawName.ToLowerInvariant();

        if (lower.Contains("concrete")) return "concrete";
        if (lower.Contains("steel") || lower.Contains("metal")) return "steel";
        if (lower.Contains("clt") || lower.Contains("cross") && lower.Contains("laminated")) return "clt";
        if (lower.Contains("wood") || lower.Contains("lumber")) return "wood";
        if (lower.Contains("glass")) return "glass";
        if (lower.Contains("aluminum") || lower.Contains("aluminium")) return "aluminum";
        if (lower.Contains("brick")) return "brick";
        if (lower.Contains("gypsum")) return "gypsum";
        if (lower.Contains("copper")) return "copper";
        if (lower.Contains("stone")) return "stone";
        if (lower.Contains("insulation")) return "insulation";
        if (lower.Contains("pvc")) return "pvc";
        if (lower.Contains("ceramic")) return "ceramic";
        if (lower.Contains("fiber")) return "fiber_cement";

        return "composite";
    }
}
