using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public interface IFieldExtractor
{
    IReadOnlyList<string> FieldNames { get; }
    IReadOnlyList<string> ComputedFieldNames => [];
    Dictionary<string, string?> Extract(Element element);
}
