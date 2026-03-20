using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

class IdMap
{
    readonly Dictionary<string, ElementId> _map = new();

    public void Register(string shortId, ElementId elementId) => _map[shortId] = elementId;

    public ElementId? Resolve(Document doc, string? shortId)
    {
        if (string.IsNullOrEmpty(shortId)) return null;
        if (_map.TryGetValue(shortId, out var id)) return id;
        return null;
    }

    public Level? ResolveLevel(Document doc, string? shortId)
    {
        var id = Resolve(doc, shortId);
        return id is not null ? doc.GetElement(id) as Level : null;
    }
}
