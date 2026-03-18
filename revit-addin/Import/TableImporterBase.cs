using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

abstract class TableImporterBase(string tableName, int order, BuiltInCategory[] categories, Func<Element, bool>? filter = null)
    : ITableImporter
{
    public string TableName => tableName;
    public int Order => order;

    protected IdMap IdMap { get; private set; } = new();

    public void SetIdMap(IdMap idMap) => IdMap = idMap;

    public ImportResult Import(Document doc, List<Dictionary<string, string?>> csvRows)
    {
        // Collect model elements for this category
        var modelElements = new List<Element>();
        foreach (var category in categories)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            foreach (var element in collector)
            {
                if (filter is not null && !filter(element)) continue;
                modelElements.Add(element);
            }
        }

        var diff = DiffEngine.Diff(csvRows, modelElements);
        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var deleted = 0;

        // Delete
        foreach (var element in diff.ToDelete)
        {
            try
            {
                doc.Delete(element.Id);
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"Delete {element.UniqueId}: {ex.Message}");
            }
        }

        // Create
        foreach (var row in diff.ToCreate)
        {
            try
            {
                var newElement = CreateElement(doc, row);
                if (newElement is not null)
                {
                    var csvId = row.GetValueOrDefault("id");
                    if (csvId is not null)
                        IdMap.Register(csvId, newElement.Id);
                    created++;
                }
            }
            catch (Exception ex)
            {
                var id = row.GetValueOrDefault("id") ?? "?";
                errors.Add($"Create {id}: {ex.Message}");
            }
        }

        // Update
        foreach (var (row, element) in diff.ToUpdate)
        {
            try
            {
                UpdateElement(doc, row, element);
                updated++;
            }
            catch (Exception ex)
            {
                errors.Add($"Update {element.UniqueId}: {ex.Message}");
            }
        }

        return new ImportResult(created, updated, deleted, errors);
    }

    protected abstract Element? CreateElement(Document doc, Dictionary<string, string?> row);
    protected abstract void UpdateElement(Document doc, Dictionary<string, string?> row, Element element);

    internal static void SetMark(Element element, Dictionary<string, string?> row)
    {
        var number = row.GetValueOrDefault("number");
        if (number is not null)
            element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(number);
    }
}
