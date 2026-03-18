using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public interface IFieldExtractor
{
    IReadOnlyList<string> FieldNames { get; }
    Dictionary<string, string?> Extract(Element element);
}
