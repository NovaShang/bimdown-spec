namespace BimDown.RevitAddin.Svg;

enum SvgRenderType { Line, Point, Polygon, Hosted }

record SvgTableMapping(string TableName, string SvgFileName, SvgRenderType RenderType)
{
    public static IReadOnlyList<SvgTableMapping> All { get; } =
    [
        // Architecture
        new("wall", "walls.svg", SvgRenderType.Line),
        new("column", "columns.svg", SvgRenderType.Point),
        new("slab", "slabs.svg", SvgRenderType.Polygon),
        new("space", "spaces.svg", SvgRenderType.Polygon),
        new("door", "doors.svg", SvgRenderType.Hosted),
        new("window", "windows.svg", SvgRenderType.Hosted),
        new("stair", "stairs.svg", SvgRenderType.Line),
        // Structure
        new("structure_wall", "structure_walls.svg", SvgRenderType.Line),
        new("structure_column", "structure_columns.svg", SvgRenderType.Point),
        new("structure_slab", "structure_slabs.svg", SvgRenderType.Polygon),
        new("beam", "beams.svg", SvgRenderType.Line),
        new("brace", "braces.svg", SvgRenderType.Line),
        new("isolated_foundation", "isolated_foundations.svg", SvgRenderType.Point),
        new("strip_foundation", "strip_foundations.svg", SvgRenderType.Line),
        new("raft_foundation", "raft_foundations.svg", SvgRenderType.Polygon),
        // MEP
        new("duct", "ducts.svg", SvgRenderType.Line),
        new("pipe", "pipes.svg", SvgRenderType.Line),
        new("cable_tray", "cable_trays.svg", SvgRenderType.Line),
        new("conduit", "conduits.svg", SvgRenderType.Line),
        new("equipment", "equipments.svg", SvgRenderType.Point),
        new("terminal", "terminals.svg", SvgRenderType.Point),
    ];

    static readonly Dictionary<string, SvgTableMapping> ByTable =
        All.ToDictionary(m => m.TableName);

    public static SvgTableMapping? ForTable(string tableName) =>
        ByTable.GetValueOrDefault(tableName);
}
