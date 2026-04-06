using Autodesk.Revit.DB;
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
            }),
        e => e is Wall w && w.WallType.Kind == WallKind.Basic &&
             w.StructuralUsage == Autodesk.Revit.DB.Structure.StructuralWallUsage.NonBearing);

    public static ITableExporter Column() => new TableExporter(
        "column",
        [BuiltInCategory.OST_Columns],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPointElement(), new VerticalSpanExtractor(), new MaterializedExtractor(), new SectionProfileExtractor()]));

    public static ITableExporter Slab() => new TableExporter(
        "slab",
        [BuiltInCategory.OST_Floors],
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
                return fields;
            }),
        e =>
        {
            if (e is Floor floor)
            {
                var structural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger();
                return structural != 1;
            }
            return false;
        });

    public static ITableExporter Roof() => new TableExporter(
        "roof",
        [BuiltInCategory.OST_Roofs],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPolygonElement(), new MaterializedExtractor()],
            ["roof_type", "slope", "thickness"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var slopeVal = e.get_Parameter(BuiltInParameter.ROOF_SLOPE)?.AsDouble();
                if (slopeVal is { } s && Math.Abs(s) > 1e-6)
                {
                    fields["slope"] = UnitConverter.FormatDouble(Math.Atan(s) * 180 / Math.PI);
                    fields["roof_type"] = "gable";
                }
                else
                {
                    fields["slope"] = "0";
                    fields["roof_type"] = "flat";
                }
                var thickness = e.get_Parameter(BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM)?.AsDouble();
                fields["thickness"] = thickness is { } t ? UnitConverter.FormatDouble(UnitConverter.Length(t)) : null;

                // Override polygon: footprint sketch or slab shape boundary
                // (PolygonElementExtractor uses top solid face which is wrong for sloped roofs)
                var footprint = GetRoofFootprint(e) ?? GetRoofBoundaryFromSlabShape(e) ?? GetRoofOutlineFromSolid(e);
                if (footprint is not null)
                {
                    fields["points"] = GeometryUtils.SerializePolygon(footprint.Value.Points);
                    if (footprint.Value.HasCurvedEdges)
                        fields["_has_curved_edges"] = "true";
                }
                return fields;
            }));

    static (IList<XYZ> Points, bool HasCurvedEdges)? GetRoofBoundaryFromSlabShape(Element element)
    {
        if (element is not RoofBase roof) return null;

        var editor = roof.GetSlabShapeEditor();
        if (editor is null || !editor.IsEnabled) return null;

        // Collect boundary edges from slab shape creases
        var edges = new List<(XYZ Start, XYZ End)>();
        foreach (SlabShapeCrease crease in editor.SlabShapeCreases)
        {
            if (crease.CreaseType == SlabShapeCreaseType.Boundary)
            {
                var curve = crease.Curve;
                edges.Add((curve.GetEndPoint(0), curve.GetEndPoint(1)));
            }
        }

        if (edges.Count < 3) return null;

        // Chain boundary edges into an ordered polygon
        var points = new List<XYZ> { edges[0].Start };
        var current = edges[0].End;
        var used = new HashSet<int> { 0 };

        while (used.Count < edges.Count)
        {
            points.Add(current);
            var found = false;
            for (var i = 0; i < edges.Count; i++)
            {
                if (used.Contains(i)) continue;
                if (edges[i].Start.DistanceTo(current) < 1e-6)
                {
                    current = edges[i].End;
                    used.Add(i);
                    found = true;
                    break;
                }
                if (edges[i].End.DistanceTo(current) < 1e-6)
                {
                    current = edges[i].Start;
                    used.Add(i);
                    found = true;
                    break;
                }
            }
            if (!found) break;
        }

        return points.Count >= 3 ? (points, false) : null;
    }

    /// <summary>
    /// Extracts roof outline from solid geometry by finding outer boundary edges
    /// (edges not shared between two upward-facing faces).
    /// </summary>
    static (IList<XYZ> Points, bool HasCurvedEdges)? GetRoofOutlineFromSolid(Element element)
    {
        var opt = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Coarse };
        var geom = element.get_Geometry(opt);
        if (geom is null) return null;

        // Collect all upward-facing faces from all solids
        var topFaces = new List<PlanarFace>();
        foreach (var obj in geom)
        {
            var solid = obj as Solid ?? (obj as GeometryInstance)?.GetInstanceGeometry()
                .OfType<Solid>().FirstOrDefault(s => s.Faces.Size > 0);
            if (solid is null) continue;
            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace planar && planar.FaceNormal.Z > 0.1)
                    topFaces.Add(planar);
            }
        }

        if (topFaces.Count == 0) return null;

        // Collect all edges, keyed by XY-projected endpoints to find boundary vs shared
        // An edge shared by two top faces is internal (ridge/hip); unique edges form the outline
        var edgeCount = new Dictionary<string, List<(XYZ Start, XYZ End, bool IsCurved)>>();
        foreach (var face in topFaces)
        {
            foreach (var loop in face.GetEdgesAsCurveLoops())
            {
                foreach (var curve in loop)
                {
                    var s = curve.GetEndPoint(0);
                    var e = curve.GetEndPoint(1);
                    // Key by sorted XY-projected endpoints to match edges regardless of direction
                    var key = MakeEdgeKey(s, e);
                    if (!edgeCount.TryGetValue(key, out var list))
                    {
                        list = [];
                        edgeCount[key] = list;
                    }
                    list.Add((s, e, curve is not Line));
                }
            }
        }

        // Boundary edges appear exactly once among top faces
        var boundaryEdges = new List<(XYZ Start, XYZ End, bool IsCurved)>();
        foreach (var (_, list) in edgeCount)
        {
            if (list.Count == 1)
                boundaryEdges.Add(list[0]);
        }

        if (boundaryEdges.Count < 3) return null;

        // Project to XY and chain into polygon
        var edges = boundaryEdges.Select(e =>
            (Start: new XYZ(e.Start.X, e.Start.Y, 0),
             End: new XYZ(e.End.X, e.End.Y, 0),
             e.IsCurved)).ToList();

        var points = new List<XYZ> { edges[0].Start };
        var current = edges[0].End;
        var hasCurvedEdges = edges[0].IsCurved;
        var used = new HashSet<int> { 0 };

        while (used.Count < edges.Count)
        {
            points.Add(current);
            var found = false;
            for (var i = 0; i < edges.Count; i++)
            {
                if (used.Contains(i)) continue;
                if (edges[i].Start.DistanceTo(current) < 1e-6)
                {
                    current = edges[i].End;
                    if (edges[i].IsCurved) hasCurvedEdges = true;
                    used.Add(i);
                    found = true;
                    break;
                }
                if (edges[i].End.DistanceTo(current) < 1e-6)
                {
                    current = edges[i].Start;
                    if (edges[i].IsCurved) hasCurvedEdges = true;
                    used.Add(i);
                    found = true;
                    break;
                }
            }
            if (!found) break;
        }

        return points.Count >= 3 ? (points, hasCurvedEdges) : null;
    }

    static string MakeEdgeKey(XYZ a, XYZ b)
    {
        // Round to avoid floating-point noise, sort so edge direction doesn't matter
        var ax = Math.Round(a.X, 4); var ay = Math.Round(a.Y, 4);
        var bx = Math.Round(b.X, 4); var by = Math.Round(b.Y, 4);
        var s1 = $"{ax},{ay}";
        var s2 = $"{bx},{by}";
        return string.Compare(s1, s2, StringComparison.Ordinal) < 0 ? $"{s1}|{s2}" : $"{s2}|{s1}";
    }

    static (IList<XYZ> Points, bool HasCurvedEdges)? GetRoofFootprint(Element element)
    {
        if (element is not FootPrintRoof fpRoof) return null;

        var profiles = fpRoof.GetProfiles();
        if (profiles.Size == 0) return null;

        // Collect all curves from all profile arrays (Revit splits edges by slope definition)
        var edges = new List<(XYZ Start, XYZ End, bool IsCurved)>();
        foreach (ModelCurveArray profile in profiles)
        {
            foreach (ModelCurve mc in profile)
            {
                var curve = mc.GeometryCurve;
                edges.Add((curve.GetEndPoint(0), curve.GetEndPoint(1), curve is not Line));
            }
        }

        if (edges.Count < 3) return null;

        // Chain edges into an ordered polygon by endpoint matching
        var points = new List<XYZ> { edges[0].Start };
        var current = edges[0].End;
        var hasCurvedEdges = edges[0].IsCurved;
        var used = new HashSet<int> { 0 };

        while (used.Count < edges.Count)
        {
            points.Add(current);
            var found = false;
            for (var i = 0; i < edges.Count; i++)
            {
                if (used.Contains(i)) continue;
                if (edges[i].Start.DistanceTo(current) < 1e-6)
                {
                    current = edges[i].End;
                    if (edges[i].IsCurved) hasCurvedEdges = true;
                    used.Add(i);
                    found = true;
                    break;
                }
                if (edges[i].End.DistanceTo(current) < 1e-6)
                {
                    current = edges[i].Start;
                    if (edges[i].IsCurved) hasCurvedEdges = true;
                    used.Add(i);
                    found = true;
                    break;
                }
            }
            if (!found) break;
        }

        return points.Count >= 3 ? (points, hasCurvedEdges) : null;
    }

    public static ITableExporter Ceiling() => new TableExporter(
        "ceiling",
        [BuiltInCategory.OST_Ceilings],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandPolygonElement(), new MaterializedExtractor()],
            ["height_offset"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                var offset = e.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM)?.AsDouble();
                fields["height_offset"] = offset is { } o ? UnitConverter.FormatDouble(UnitConverter.Length(o)) : null;
                return fields;
            }));

    public static ITableExporter Opening() => new OpeningTableExporter();

    public static ITableExporter Space() => new TableExporter(
        "space",
        [BuiltInCategory.OST_Rooms],
        new CompositeExtractor(
            [new ElementExtractor()],
            ["x", "y", "name"],
            e =>
            {
                var fields = new Dictionary<string, string?> { ["name"] = e.Name };
                if (e.Location is LocationPoint lp)
                {
                    fields["x"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.X));
                    fields["y"] = UnitConverter.FormatDouble(UnitConverter.Length(lp.Point.Y));
                }
                return fields;
            }));

    public static ITableExporter Door() => new TableExporter(
        "door",
        [BuiltInCategory.OST_Doors],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandHostedElement(), new MaterializedExtractor()],
            ["width", "height", "operation", "hinge_position", "swing_side"],
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
                // Hinge side and swing direction from FamilyInstance flip state
                if (e is FamilyInstance fi)
                {
                    fields["hinge_position"] = fi.HandFlipped ? "end" : "start";
                    fields["swing_side"] = fi.FacingFlipped ? "right" : "left";
                }
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

    public static ITableExporter CurtainWall() => new TableExporter(
        "curtain_wall",
        [BuiltInCategory.OST_Walls],
        new CompositeExtractor(
            [..CompositeExtractor.ExpandLineElement(), new VerticalSpanExtractor(), new MaterializedExtractor()],
            ["u_grid_count", "v_grid_count", "u_spacing", "v_spacing", "panel_count", "panel_material"],
            e =>
            {
                var fields = new Dictionary<string, string?>();
                if (e is not Wall w || w.CurtainGrid is not { } grid) return fields;

                fields["u_grid_count"] = grid.NumULines.ToString();
                fields["v_grid_count"] = grid.NumVLines.ToString();
                fields["u_spacing"] = ComputeUniformSpacing(grid.GetUGridLineIds(), e.Document);
                fields["v_spacing"] = ComputeUniformSpacing(grid.GetVGridLineIds(), e.Document);

                var panelIds = grid.GetPanelIds();
                fields["panel_count"] = panelIds.Count.ToString();
                fields["panel_material"] = GetDominantPanelMaterial(e.Document, panelIds);

                return fields;
            },
            ["panel_count"]),
        e => e is Wall w && w.WallType.Kind == WallKind.Curtain);

    static string? ComputeUniformSpacing(ICollection<ElementId> gridLineIds, Document doc)
    {
        if (gridLineIds.Count < 2) return null;

        // Get midpoints of each grid line's full curve
        var midpoints = gridLineIds
            .Select(id => doc.GetElement(id) as CurtainGridLine)
            .Where(gl => gl is not null)
            .Select(gl => gl!.FullCurve.Evaluate(0.5, true))
            .ToList();

        if (midpoints.Count < 2) return null;

        // Grid lines are parallel — project onto perpendicular direction to get 1D offsets
        // Perpendicular = direction from first to second midpoint (approximately)
        var dir = (midpoints[1] - midpoints[0]).Normalize();
        var offsets = midpoints.Select(p => dir.DotProduct(p)).OrderBy(o => o).ToList();

        var spacing = offsets[1] - offsets[0];
        for (var i = 2; i < offsets.Count; i++)
        {
            if (Math.Abs((offsets[i] - offsets[i - 1]) - spacing) > 0.001)
                return null;
        }

        return UnitConverter.FormatDouble(UnitConverter.Length(spacing));
    }

    static string? GetDominantPanelMaterial(Document doc, ICollection<ElementId> panelIds)
    {
        return panelIds
            .Select(id => doc.GetElement(id))
            .Where(p => p is not null)
            .SelectMany(p => p.GetMaterialIds(false))
            .Select(id => doc.GetElement(id) as Material)
            .Where(m => m is not null)
            .GroupBy(m => m!.Name)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;
    }

    public static ITableExporter Ramp() => new TableExporter(
        "ramp",
        [BuiltInCategory.OST_Ramps],
        new CompositeExtractor(
            CompositeExtractor.ExpandSpatialLineElement(),
            ["width"],
            e =>
            {
                var width = e.get_Parameter(BuiltInParameter.STAIRS_ATTR_TREAD_WIDTH).AsPositiveDouble()
                         ?? FindDoubleParameterByNames(e, "width", "w", "宽");
                return new Dictionary<string, string?>
                {
                    ["width"] = width is { } w ? UnitConverter.FormatDouble(UnitConverter.Length(w)) : null
                };
            }));

    public static ITableExporter Railing() => new TableExporter(
        "railing",
        [BuiltInCategory.OST_StairsRailing],
        new CompositeExtractor(
            CompositeExtractor.ExpandSpatialLineElement(),
            ["height"],
            e =>
            {
                var height = FindDoubleParameterByNames(e, "height", "h", "高", "Top Rail Height");
                return new Dictionary<string, string?>
                {
                    ["height"] = height is { } h ? UnitConverter.FormatDouble(UnitConverter.Length(h)) : null
                };
            }));

    public static ITableExporter RoomSeparator() => new TableExporter(
        "room_separator",
        [BuiltInCategory.OST_RoomSeparationLines],
        new CompositeExtractor(CompositeExtractor.ExpandLineElement()));

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
            if (lowerOp.Contains("revolving") || lowerOp.Contains("旋转")) return "revolving";
            return op;
        }

        var name = (e.Name + " " + (e as FamilyInstance)?.Symbol.Family.Name).ToLowerInvariant();
        if (name.Contains("single") || name.Contains("单开") || name.Contains("单扇") || name.Contains("单门")) return "single_swing";
        if (name.Contains("double") || name.Contains("双开") || name.Contains("双扇") || name.Contains("双门")) return "double_swing";
        if (name.Contains("sliding") || name.Contains("推拉")) return "sliding";
        if (name.Contains("folding") || name.Contains("折叠")) return "folding";
        if (name.Contains("revolving") || name.Contains("旋转")) return "revolving";

        return null;
    }
}
