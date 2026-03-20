using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BimDown.RevitAddin.Extractors;

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
                var w = e.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();
                var h = e.get_Parameter(BuiltInParameter.DOOR_HEIGHT)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
                fields["width"] = w is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;
                fields["height"] = h is { } hv ? UnitConverter.FormatDouble(UnitConverter.Length(hv)) : null;
                fields["operation"] = null; // Would need family name analysis to determine
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
                var w = e.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();
                var h = e.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
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
                var width = e.get_Parameter(BuiltInParameter.STAIRS_ATTR_TREAD_WIDTH)?.AsDouble();
                fields["width"] = width is { } w ? UnitConverter.FormatDouble(UnitConverter.Length(w)) : null;

                var steps = e.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_NUM_RISERS)?.AsInteger();
                fields["step_count"] = steps?.ToString();
                return fields;
            }));
}
