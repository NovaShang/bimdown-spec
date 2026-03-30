using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using BimDown.RevitAddin.Extractors;

namespace BimDown.RevitAddin.Tables;

public static class StructureTableExporters
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

    public static ITableExporter Foundation() => new TableExporter(
        "foundation",
        [BuiltInCategory.OST_StructuralFoundation],
        new CompositeExtractor(
            [new ElementExtractor(), new FoundationExtractor(), new MaterializedExtractor()]));
}
