using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

class MaterializedExtractor : IFieldExtractor
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
        return new Dictionary<string, string?> { ["material"] = materialName };
    }
}
