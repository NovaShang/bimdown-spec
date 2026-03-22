using Autodesk.Revit.DB;

namespace BimDown.RevitAddin;

class ShortIdGenerator
{
    static readonly Dictionary<string, string> PrefixMap = new()
    {
        ["level"] = "lv",
        ["grid"] = "gr",
        ["wall"] = "w",
        ["column"] = "c",
        ["slab"] = "sl",
        ["space"] = "sp",
        ["door"] = "d",
        ["window"] = "wn",
        ["stair"] = "st",
        ["structure_wall"] = "sw",
        ["structure_column"] = "sc",
        ["structure_slab"] = "ss",
        ["beam"] = "bm",
        ["brace"] = "br",
        ["isolated_foundation"] = "if",
        ["strip_foundation"] = "sf",
        ["raft_foundation"] = "rf",
        ["duct"] = "du",
        ["pipe"] = "pi",
        ["cable_tray"] = "ct",
        ["conduit"] = "co",
        ["equipment"] = "eq",
        ["terminal"] = "tm",
        ["mep_node"] = "mn",
    };

    static readonly string[] ReferenceFields = ["level_id", "host_id", "top_level_id", "start_node_id", "end_node_id"];

    readonly Dictionary<string, int> _counters = new();
    readonly Dictionary<string, string> _uidToShort = new();

    internal void SeedFromModel(IList<Element> elements)
    {
        foreach (var element in elements)
        {
            var shortId = BimDownParameter.Get(element);
            if (shortId is null) continue;

            _uidToShort[element.UniqueId] = shortId;

            var dashIdx = shortId.LastIndexOf('-');
            if (dashIdx < 0) continue;

            var prefix = shortId[..dashIdx];
            if (int.TryParse(shortId[(dashIdx + 1)..], out var num))
            {
                _counters.TryGetValue(prefix, out var current);
                if (num >= current) _counters[prefix] = num;
            }
        }
    }

    internal string GetOrAssign(string tableName, string uniqueId)
    {
        if (_uidToShort.TryGetValue(uniqueId, out var existing))
            return existing;

        var prefix = PrefixMap[tableName];
        _counters.TryGetValue(prefix, out var counter);
        counter++;
        _counters[prefix] = counter;

        var shortId = $"{prefix}-{counter}";
        _uidToShort[uniqueId] = shortId;
        return shortId;
    }

    internal string? Resolve(string? uniqueId)
    {
        if (uniqueId is null) return null;
        return _uidToShort.GetValueOrDefault(uniqueId);
    }

    internal void RemapRows(string tableName, List<Dictionary<string, string?>> rows)
    {
        foreach (var row in rows)
        {
            var uid = row.GetValueOrDefault("id");
            if (uid is not null)
                row["id"] = GetOrAssign(tableName, uid);

            foreach (var field in ReferenceFields)
            {
                var refUid = row.GetValueOrDefault(field);
                if (refUid is not null)
                    row[field] = Resolve(refUid);
            }
        }
    }

    internal IReadOnlyDictionary<string, string> Mappings => _uidToShort;
}
