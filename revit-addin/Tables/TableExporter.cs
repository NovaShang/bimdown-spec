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
    public IReadOnlyList<string> CsvColumns => extractor.CsvColumns;

    /// <summary>
    /// Optional mesh fallback set. When set, exporters can flag elements that
    /// cannot be precisely represented for GLB mesh export.
    /// </summary>
    public MeshFallbackSet? MeshFallback { get; set; }

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
                    var row = extractor.Extract(element);
                    DetectMeshFallback(element, row);
                    rows.Add(row);
                }
                catch
                {
                    // Skip elements that fail extraction
                }
            }
        }

        return rows;
    }

    void DetectMeshFallback(Element element, Dictionary<string, string?> row)
    {
        if (MeshFallback is null) return;

        // B1: Profile-edited wall
        if (element is Wall wall)
        {
            if (wall.SketchId != ElementId.InvalidElementId)
                MeshFallback.Add(element.Id, "edited_profile");

            // B2: Slanted wall
            var slantAngle = wall.get_Parameter(BuiltInParameter.WALL_SINGLE_SLANT_ANGLE_FROM_VERTICAL)?.AsDouble() ?? 0;
            if (Math.Abs(slantAngle) > 0.001)
                MeshFallback.Add(element.Id, "slanted");
        }

        // C1-C3: Curved polygon edges
        if (row.GetValueOrDefault("_has_curved_edges") == "true")
            MeshFallback.Add(element.Id, "curved_edges");

        // D: Point-based ramps/railings (no LocationCurve → geometry fields empty)
        if (tableName is "ramp" or "railing" && element.Location is not LocationCurve)
            MeshFallback.Add(element.Id, "point_based");
    }
}
