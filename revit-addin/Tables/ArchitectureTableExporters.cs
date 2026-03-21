using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BimDown.RevitAddin.Extractors;
using static BimDown.RevitAddin.Extractors.ParameterUtils;

namespace BimDown.RevitAddin.Tables;

public static class ArchitectureTableExporters
{
    public static ITableExporter Wall() => new TableExporter(
        "wall",
        [BuiltInCategory.OST_Walls],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandLineElement(), new VerticalSpanExtractor(), new MaterializedExtractor()],
            ["thickness"],
            e => new Dictionary<string, string?>
            {
                ["thickness"] = e is Wall w
                    ? UnitConverter.FormatDouble(UnitConverter.Length(w.Width))
                    : null
            },
            ["thickness"]),
        e => e is Wall w && w.WallType.Kind == WallKind.Basic &&
             w.StructuralUsage == Autodesk.Revit.DB.Structure.StructuralWallUsage.NonBearing);

    public static ITableExporter Column() => new TableExporter(
        "column",
        [BuiltInCategory.OST_Columns],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new VerticalSpanExtractor(), new MaterializedExtractor(), new SectionProfileExtractor()]));

    public static ITableExporter Slab() => new TableExporter(
        "slab",
        [BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPolygonElement(), new MaterializedExtractor()],
            ["function", "thickness"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                if (e is Floor floor)
                {
                    fields["function"] = "floor";
                    var thickness = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble();
                    fields["thickness"] = thickness is { } t ? UnitConverter.FormatDouble(UnitConverter.Length(t)) : null;
                }
                else if (e is RoofBase)
                {
                    fields["function"] = "roof";
                    var thickness = e.get_Parameter(BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM)?.AsDouble();
                    fields["thickness"] = thickness is { } t ? UnitConverter.FormatDouble(UnitConverter.Length(t)) : null;
                }
                return fields;
            }),
        e =>
        {
            // Exclude structural floors
            if (e is Floor floor)
            {
                var structural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger();
                return structural != 1;
            }
            return true; // Include all roofs as architecture slabs
        });

    public static ITableExporter Space() => new TableExporter(
        "space",
        [BuiltInCategory.OST_Rooms],
        new CompositeExtractor(
            CompositeExtractor.ExpandPolygonElement(),
            ["name"],
            e => new Dictionary<string, string?> { ["name"] = e.Name }));

    public static ITableExporter Door() => new TableExporter(
        "door",
        [BuiltInCategory.OST_Doors],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandHostedElement(), new MaterializedExtractor()],
            ["width", "height", "operation"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var w = e.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsPositiveDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM).AsPositiveDouble()
                     ?? Extractors.ParameterUtils.FindDoubleParameterByNames(e, "width", "w", "b", "宽");
                var h = e.get_Parameter(BuiltInParameter.DOOR_HEIGHT).AsPositiveDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM).AsPositiveDouble()
                     ?? Extractors.ParameterUtils.FindDoubleParameterByNames(e, "height", "depth", "h", "d", "高", "深");
                fields["width"] = w is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;
                fields["height"] = h is { } hv ? UnitConverter.FormatDouble(UnitConverter.Length(hv)) : null;
                fields["operation"] = GetDoorOperation(e);
                return fields;
            }));

    public static ITableExporter Window() => new TableExporter(
        "window",
        [BuiltInCategory.OST_Windows],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandHostedElement(), new MaterializedExtractor()],
            ["width", "height"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var w = e.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsPositiveDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM).AsPositiveDouble()
                     ?? Extractors.ParameterUtils.FindDoubleParameterByNames(e, "width", "w", "b", "宽");
                var h = e.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsPositiveDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM).AsPositiveDouble()
                     ?? Extractors.ParameterUtils.FindDoubleParameterByNames(e, "height", "depth", "h", "d", "高", "深");
                fields["width"] = w is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;
                fields["height"] = h is { } hv ? UnitConverter.FormatDouble(UnitConverter.Length(hv)) : null;
                return fields;
            }));

    public static ITableExporter Stair() => new TableExporter(
        "stair",
        [BuiltInCategory.OST_Stairs],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new VerticalSpanExtractor()],
            ["width", "step_count"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var width = e.get_Parameter(BuiltInParameter.STAIRS_ATTR_TREAD_WIDTH).AsPositiveDouble()
                         ?? e.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH)?.AsDouble();
                fields["width"] = width is { } w ? UnitConverter.FormatDouble(UnitConverter.Length(w)) : null;

                var steps = e.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_NUM_RISERS)?.AsInteger();
                fields["step_count"] = steps?.ToString();
                return fields;
            }));

    static string? GetDoorOperation(Element e)
    {
        var op = Extractors.ParameterUtils.FindStringParameterByNames(e, "Operation", "Swing", "操作", "开启");
        if (!string.IsNullOrEmpty(op))
        {
            var lowerOp = op.ToLowerInvariant();
            if (lowerOp.Contains("single") || lowerOp.Contains("单开") || lowerOp.Contains("单扇") || lowerOp.Contains("单门")) return "single_swing";
            if (lowerOp.Contains("double") || lowerOp.Contains("双开") || lowerOp.Contains("双扇") || lowerOp.Contains("双门")) return "double_swing";
            if (lowerOp.Contains("sliding") || lowerOp.Contains("推拉")) return "sliding";
            if (lowerOp.Contains("folding") || lowerOp.Contains("折叠")) return "folding";
            if (lowerOp.Contains("rolling") || lowerOp.Contains("卷帘")) return "rolling";
            return op;
        }

        var name = (e.Name + " " + (e as FamilyInstance)?.Symbol.Family.Name).ToLowerInvariant();
        if (name.Contains("single") || name.Contains("单开") || name.Contains("单扇") || name.Contains("单门")) return "single_swing";
        if (name.Contains("double") || name.Contains("双开") || name.Contains("双扇") || name.Contains("双门")) return "double_swing";
        if (name.Contains("sliding") || name.Contains("推拉")) return "sliding";
        if (name.Contains("folding") || name.Contains("折叠")) return "folding";
        if (name.Contains("rolling") || name.Contains("卷帘")) return "rolling";

        return null;
    }
}
