using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using BimDown.RevitAddin.Extractors;

namespace BimDown.RevitAddin.Tables;

static class StructureTableExporters
{
    public static ITableExporter StructureWall() => new TableExporter(
        "structure_wall",
        [BuiltInCategory.OST_Walls],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandLineElement(), new VerticalSpanExtractor(), new MaterializedExtractor()],
            ["thickness"],
            e => new Dictionary<string, string?>
            {
                ["thickness"] = e is Wall w
                    ? UnitConverter.FormatDouble(UnitConverter.Length(w.Width))
                    : null
            }),
        e => e is Wall w && w.StructuralUsage != StructuralWallUsage.NonBearing);

    public static ITableExporter StructureColumn() => new TableExporter(
        "structure_column",
        [BuiltInCategory.OST_StructuralColumns],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new VerticalSpanExtractor(), new MaterializedExtractor(), new StructuralSectionProfileExtractor()]));

    public static ITableExporter StructureSlab() => new TableExporter(
        "structure_slab",
        [BuiltInCategory.OST_Floors],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPolygonElement(), new MaterializedExtractor()],
            ["function", "thickness"],
            e =>
            {
                var fields = new Dictionary<string, string?> { ["function"] = "floor" };
                var thickness = e.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble();
                fields["thickness"] = thickness is { } t ? UnitConverter.FormatDouble(UnitConverter.Length(t)) : null;
                return fields;
            }),
        e =>
        {
            if (e is not Floor) return false;
            var structural = e.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger();
            return structural == 1;
        });

    public static ITableExporter Beam() => new TableExporter(
        "beam",
        [BuiltInCategory.OST_StructuralFraming],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new StructuralSectionProfileExtractor(), new MaterializedExtractor()]),
        e => e is FamilyInstance fi && fi.StructuralType == StructuralType.Beam);

    public static ITableExporter Brace() => new TableExporter(
        "brace",
        [BuiltInCategory.OST_StructuralFraming],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new StructuralSectionProfileExtractor(), new MaterializedExtractor()]),
        e => e is FamilyInstance fi && fi.StructuralType == StructuralType.Brace);

    public static ITableExporter IsolatedFoundation() => new TableExporter(
        "isolated_foundation",
        [BuiltInCategory.OST_StructuralFoundation],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MaterializedExtractor()],
            ["length", "width", "thickness"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var l = e.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH)?.AsDouble();
                var w = e.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();
                var t = e.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
                fields["length"] = l is { } lv ? UnitConverter.FormatDouble(UnitConverter.Length(lv)) : null;
                fields["width"] = w is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;
                fields["thickness"] = t is { } tv ? UnitConverter.FormatDouble(UnitConverter.Length(tv)) : null;
                return fields;
            }),
        e => e.Location is LocationPoint);

    public static ITableExporter StripFoundation() => new TableExporter(
        "strip_foundation",
        [BuiltInCategory.OST_StructuralFoundation],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandLineElement(), new MaterializedExtractor()],
            ["width", "thickness"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var w = e.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();
                var t = e.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble()
                     ?? e.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
                fields["width"] = w is { } wv ? UnitConverter.FormatDouble(UnitConverter.Length(wv)) : null;
                fields["thickness"] = t is { } tv ? UnitConverter.FormatDouble(UnitConverter.Length(tv)) : null;
                return fields;
            }),
        e => e.Location is LocationCurve);

    public static ITableExporter RaftFoundation() => new TableExporter(
        "raft_foundation",
        [BuiltInCategory.OST_StructuralFoundation],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPolygonElement(), new MaterializedExtractor()],
            ["thickness"],
            e =>
            {
                var thickness = e.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble();
                return new Dictionary<string, string?>
                {
                    ["thickness"] = thickness is { } t ? UnitConverter.FormatDouble(UnitConverter.Length(t)) : null
                };
            }),
        e => e is Floor);
}
