using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class MepNodeExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = [];

    public Dictionary<string, string?> Extract(Element element)
    {
        // mep_node itself has no extra fields beyond point_element and mep_system
        // The point extraction is handled by PointElementExtractor (which we'll compose)
        // and the system is handled by MepSystemExtractor.
        return [];
    }
}
