using Autodesk.Revit.DB;
using BimDown.RevitAddin.Extractors;

namespace BimDown.RevitAddin.Tables;

public static class MepTableExporters
{
    public static ITableExporter Duct() => new TableExporter(
        "duct",
        [BuiltInCategory.OST_DuctCurves],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter Pipe() => new TableExporter(
        "pipe",
        [BuiltInCategory.OST_PipeCurves],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter CableTray() => new TableExporter(
        "cable_tray",
        [BuiltInCategory.OST_CableTray],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter Conduit() => new TableExporter(
        "conduit",
        [BuiltInCategory.OST_Conduit],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandSpatialLineElement(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter MepNode() => new TableExporter(
        "mep_node",
        [BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_CableTrayFitting, BuiltInCategory.OST_ConduitFitting],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MepSystemExtractor(), new MepNodeExtractor()]));

    public static ITableExporter Equipment() => new TableExporter(
        "equipment",
        [BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_ElectricalEquipment],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MepSystemExtractor()],
            ["equipment_type"],
            e => new Dictionary<string, string?> { ["equipment_type"] = null })); // Would need family name analysis

    public static ITableExporter Terminal() => new TableExporter(
        "terminal",
        [BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_ElectricalFixtures],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MepSystemExtractor()],
            ["terminal_type"],
            e => new Dictionary<string, string?> { ["terminal_type"] = null })); // Would need family name analysis
}
