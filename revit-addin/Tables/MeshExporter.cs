using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Tables;

class MeshExporter : ITableExporter
{
    static readonly (BuiltInCategory Category, string EnumValue)[] CategoryMap =
    [
        (BuiltInCategory.OST_StairsRailing, "railing"),
        (BuiltInCategory.OST_GenericModel, "generic_model"),
        (BuiltInCategory.OST_Topography, "topography"),
        (BuiltInCategory.OST_Planting, "planting"),
        (BuiltInCategory.OST_Site, "site"),
    ];

    public string TableName => "mesh";

    public IReadOnlyList<string> Columns { get; } =
        ["id", "category", "name", "level_id", "mesh_file", "x", "y", "z", "rotation"];

    public IReadOnlyList<string> CsvColumns => Columns;

    // Store element references for GLB export after ID remapping
    readonly Dictionary<string, Element> _elementsByUid = new();

    public List<Dictionary<string, string?>> Export(Document doc)
    {
        var rows = new List<Dictionary<string, string?>>();

        foreach (var (category, enumValue) in CategoryMap)
        {
            try
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                foreach (var element in collector)
                {
                    try
                    {
                        var row = ExtractRow(element, enumValue);
                        if (row is not null)
                        {
                            _elementsByUid[element.UniqueId] = element;
                            rows.Add(row);
                        }
                    }
                    catch
                    {
                        // Skip elements that fail extraction
                    }
                }
            }
            catch
            {
                // Skip categories that fail collection
            }
        }

        return rows;
    }

    /// <summary>
    /// Export GLB files for all mesh elements. Call after ID remapping so short IDs are used for filenames.
    /// Updates the mesh_file field in each row.
    /// </summary>
    internal void ExportGlbFiles(string outputDir, List<Dictionary<string, string?>> rows,
        IReadOnlyDictionary<string, string> uidToShortId)
    {
        foreach (var row in rows)
        {
            var shortId = row.GetValueOrDefault("id");
            if (shortId is null) continue;

            // Find the original element — look up the UID from the reverse mapping
            var uid = uidToShortId
                .FirstOrDefault(kvp => kvp.Value == shortId).Key;
            if (uid is null || !_elementsByUid.TryGetValue(uid, out var element)) continue;

            try
            {
                var meshPath = GlbExporter.ExportElement(element, outputDir, shortId);
                row["mesh_file"] = meshPath ?? "";
            }
            catch
            {
                row["mesh_file"] = "";
            }
        }
    }

    static Dictionary<string, string?>? ExtractRow(Element element, string categoryEnum)
    {
        var row = new Dictionary<string, string?>
        {
            ["id"] = element.UniqueId,
            ["category"] = categoryEnum,
            ["name"] = element.Name,
            ["mesh_file"] = "", // placeholder, filled in by ExportGlbFiles
        };

        // Level
        var levelId = element.LevelId;
        if (levelId != ElementId.InvalidElementId)
            row["level_id"] = element.Document.GetElement(levelId)?.UniqueId;

        // Position from Location or BoundingBox center
        if (element.Location is LocationPoint lp)
        {
            row["x"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.X));
            row["y"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.Y));
            row["z"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.Z));
            row["rotation"] = UnitConverter.FormatDouble(lp.Rotation * 180 / Math.PI);
        }
        else if (element.Location is LocationCurve lc)
        {
            var mid = lc.Curve.Evaluate(0.5, true);
            row["x"] = UnitConverter.FormatDouble(UnitConverter.Length(mid.X));
            row["y"] = UnitConverter.FormatDouble(UnitConverter.Length(mid.Y));
            row["z"] = UnitConverter.FormatDouble(UnitConverter.Length(mid.Z));
            row["rotation"] = "0";
        }
        else
        {
            var bb = element.get_BoundingBox(null);
            if (bb is not null)
            {
                var center = (bb.Min + bb.Max) / 2;
                row["x"] = UnitConverter.FormatDouble(UnitConverter.Length(center.X));
                row["y"] = UnitConverter.FormatDouble(UnitConverter.Length(center.Y));
                row["z"] = UnitConverter.FormatDouble(UnitConverter.Length(center.Z));
            }
            else
            {
                row["x"] = "0";
                row["y"] = "0";
                row["z"] = "0";
            }
            row["rotation"] = "0";
        }

        return row;
    }
}
