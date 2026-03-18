using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

interface IFieldExtractor
{
    IReadOnlyList<string> FieldNames { get; }
    Dictionary<string, string?> Extract(Element element);
}
