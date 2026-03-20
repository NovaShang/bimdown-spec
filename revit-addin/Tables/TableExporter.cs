using Autodesk.Revit.DB;
using BimDown.RevitAddin.Extractors;

namespace BimDown.RevitAddin.Tables;

public class TableExporter(
    string tableName,
    BuiltInCategory[] categories,
    CompositeExtractor extractor,
    Func<Element, bool>? filter = null) : ITableExporter
{
    public string TableName => tableName;
    public IReadOnlyList<string> Columns => extractor.FieldNames;

    public List<Dictionary<string, string?>> Export(Document doc)
    {
        var rows = new List<Dictionary<string, string?>>();

        foreach (var category in categories)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            foreach (var element in collector.OrderBy(e => e.Id.Value))
            {
                try
                {
                    if (filter is not null && !filter(element)) continue;
                    rows.Add(extractor.Extract(element));
                }
                catch
                {
                    // Skip elements that fail extraction
                }
            }
        }

        return rows;
    }
}
