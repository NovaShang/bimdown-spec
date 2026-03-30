using Autodesk.Revit.DB;
using BimDown.RevitAddin.Extractors;

namespace BimDown.RevitAddin.Tables;

public static class MepTableExporters
{
    /// <summary>
    /// Builds the extractor chain for MEP curves (duct, pipe, cable_tray, conduit).
    /// Uses MepCurveGeometryExtractor (connector-based endpoints) instead of the
    /// generic LineElement/SpatialLineElement extractors.
    /// </summary>
    static IFieldExtractor[] ExpandMepCurve() =>
        [new ElementExtractor(), new MepCurveGeometryExtractor()];

    public static ITableExporter Duct() => new TableExporter(
        "duct",
        [BuiltInCategory.OST_DuctCurves],
        new CompositeExtractor(
            [..ExpandMepCurve(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter Pipe() => new TableExporter(
        "pipe",
        [BuiltInCategory.OST_PipeCurves],
        new CompositeExtractor(
            [..ExpandMepCurve(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter CableTray() => new TableExporter(
        "cable_tray",
        [BuiltInCategory.OST_CableTray],
        new CompositeExtractor(
            [..ExpandMepCurve(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter Conduit() => new TableExporter(
        "conduit",
        [BuiltInCategory.OST_Conduit],
        new CompositeExtractor(
            [..ExpandMepCurve(), new SectionProfileExtractor(), new MepSystemExtractor(), new MepConnectedSegmentExtractor()]));

    public static ITableExporter MepNode() => new TableExporter(
        "mep_node",
        [BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_CableTrayFitting, BuiltInCategory.OST_ConduitFitting,
         BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_PipeAccessory],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MepSystemExtractor(), new MepNodeExtractor()]));

    public static ITableExporter Equipment() => new TableExporter(
        "equipment",
        [BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_ElectricalEquipment],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MepSystemExtractor()],
            ["equipment_type"],
            e => new Dictionary<string, string?> { ["equipment_type"] = null }));

    public static ITableExporter Terminal() => new TableExporter(
        "terminal",
        [BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_ElectricalFixtures],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new MepSystemExtractor()],
            ["terminal_type"],
            e => new Dictionary<string, string?> { ["terminal_type"] = null }));
}
