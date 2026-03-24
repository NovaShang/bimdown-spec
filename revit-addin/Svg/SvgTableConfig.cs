namespace BimDown.RevitAddin.Svg;

enum SvgRenderType { Line, Point, Polygon, Hosted }

record SvgTableMapping(string TableName, string SvgFileName, SvgRenderType RenderType)
{
    public static IReadOnlyList<SvgTableMapping> All { get; } =
    [
        // Architecture
        new("wall", "wall.svg", SvgRenderType.Line),
        new("column", "column.svg", SvgRenderType.Point),
        new("slab", "slab.svg", SvgRenderType.Polygon),
        new("space", "space.svg", SvgRenderType.Polygon),
        new("door", "door.svg", SvgRenderType.Hosted),
        new("window", "window.svg", SvgRenderType.Hosted),
        new("stair", "stair.svg", SvgRenderType.Line),
        new("curtain_wall", "curtain_wall.svg", SvgRenderType.Line),
        // Structure
        new("structure_wall", "structure_wall.svg", SvgRenderType.Line),
        new("structure_column", "structure_column.svg", SvgRenderType.Point),
        new("structure_slab", "structure_slab.svg", SvgRenderType.Polygon),
        new("beam", "beam.svg", SvgRenderType.Line),
        new("brace", "brace.svg", SvgRenderType.Line),
        new("isolated_foundation", "isolated_foundation.svg", SvgRenderType.Point),
        new("strip_foundation", "strip_foundation.svg", SvgRenderType.Line),
        new("raft_foundation", "raft_foundation.svg", SvgRenderType.Polygon),
        // MEP
        new("duct", "duct.svg", SvgRenderType.Line),
        new("pipe", "pipe.svg", SvgRenderType.Line),
        new("cable_tray", "cable_tray.svg", SvgRenderType.Line),
        new("conduit", "conduit.svg", SvgRenderType.Line),
        new("equipment", "equipment.svg", SvgRenderType.Point),
        new("terminal", "terminal.svg", SvgRenderType.Point),
        new("mep_node", "mep_node.svg", SvgRenderType.Point),
    ];

    static readonly Dictionary<string, SvgTableMapping> ByTable =
        All.ToDictionary(m => m.TableName);

    public static SvgTableMapping? ForTable(string tableName) =>
        ByTable.GetValueOrDefault(tableName);
}
