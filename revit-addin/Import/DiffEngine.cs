using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

static class DiffEngine
{
    public static DiffResult Diff(List<Dictionary<string, string?>> csvRows, IList<Element> modelElements, IReadOnlyDictionary<string, string>? uuidToIdMap = null)
    {
        var modelById = new Dictionary<string, Element>();
        foreach (var el in modelElements)
        {
            if (uuidToIdMap is not null && uuidToIdMap.TryGetValue(el.UniqueId, out var mappedId))
            {
                modelById[mappedId] = el;
            }
            else
            {
                var bid = BimDownParameter.Get(el);
                if (bid is not null) modelById[bid] = el;
            }
        }

        var toUpdate = new List<(Dictionary<string, string?> Row, Element Element)>();
        var toCreate = new List<Dictionary<string, string?>>();
        var matchedIds = new HashSet<string>();

        foreach (var row in csvRows)
        {
            var id = row.GetValueOrDefault("id");
            if (id is not null && modelById.TryGetValue(id, out var element))
            {
                toUpdate.Add((row, element));
                matchedIds.Add(id);
            }
            else
            {
                toCreate.Add(row);
            }
        }

        var toDelete = modelElements
            .Where(el =>
            {
                var bid = (uuidToIdMap is not null && uuidToIdMap.TryGetValue(el.UniqueId, out var mappedId))
                    ? mappedId
                    : BimDownParameter.Get(el);
                return bid is not null && !matchedIds.Contains(bid);
            })
            .ToList();

        return new DiffResult(toUpdate, toCreate, toDelete);
    }
}
