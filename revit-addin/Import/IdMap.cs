using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

class IdMap
{
    readonly Dictionary<string, ElementId> _map = new();

    public void Register(string csvId, ElementId elementId) => _map[csvId] = elementId;

    public ElementId? Resolve(Document doc, string? csvId)
    {
        if (string.IsNullOrEmpty(csvId)) return null;

        // Check map first (for newly created elements)
        if (_map.TryGetValue(csvId, out var mapped)) return mapped;

        // Fall back to document lookup by UniqueId
        var element = doc.GetElement(csvId);
        return element?.Id;
    }

    public Level? ResolveLevel(Document doc, string? csvId)
    {
        var id = Resolve(doc, csvId);
        return id is not null ? doc.GetElement(id) as Level : null;
    }
}
