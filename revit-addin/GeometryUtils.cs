using System.Text;
using Autodesk.Revit.DB;

namespace BimDown.RevitAddin;

static class GeometryUtils
{
    public static (XYZ min, XYZ max)? GetBoundingBox(Element element)
    {
        var bb = element.get_BoundingBox(null);
        if (bb is null) return null;
        return (bb.Min, bb.Max);
    }

    public static void WriteBoundingBox(Dictionary<string, string?> fields, Element element)
    {
        var bb = GetBoundingBox(element);
        if (bb is { } box)
        {
            fields["bbox_min_x"] = UnitConverter.FormatDouble(UnitConverter.Length(box.min.X));
            fields["bbox_min_y"] = UnitConverter.FormatDouble(UnitConverter.Length(box.min.Y));
            fields["bbox_min_z"] = UnitConverter.FormatDouble(UnitConverter.Length(box.min.Z));
            fields["bbox_max_x"] = UnitConverter.FormatDouble(UnitConverter.Length(box.max.X));
            fields["bbox_max_y"] = UnitConverter.FormatDouble(UnitConverter.Length(box.max.Y));
            fields["bbox_max_z"] = UnitConverter.FormatDouble(UnitConverter.Length(box.max.Z));
        }
    }

    /// <summary>
    /// Serializes polygon points to JSON format: [[x1,y1],[x2,y2],...]
    /// Points are converted from feet to meters.
    /// </summary>
    public static string SerializePolygon(IList<XYZ> points)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var p = points[i];
            sb.Append('[')
              .Append(UnitConverter.FormatDouble(UnitConverter.Length(p.X)))
              .Append(',')
              .Append(UnitConverter.FormatDouble(UnitConverter.Length(p.Y)))
              .Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the largest downward-facing PlanarFace from an element's geometry.
    /// Returns the outer edge loop points.
    /// </summary>
    public static IList<XYZ>? GetTopFacePolygon(Element element)
    {
        var opt = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Coarse };
        var geom = element.get_Geometry(opt);
        if (geom is null) return null;

        PlanarFace? bestFace = null;
        double bestArea = 0;

        foreach (var obj in geom)
        {
            if (obj is Solid solid)
            {
                foreach (Face face in solid.Faces)
                {
                    if (face is PlanarFace planar && planar.FaceNormal.Z > 0.5)
                    {
                        if (planar.Area > bestArea)
                        {
                            bestArea = planar.Area;
                            bestFace = planar;
                        }
                    }
                }
            }
            else if (obj is GeometryInstance inst)
            {
                foreach (var subObj in inst.GetInstanceGeometry())
                {
                    if (subObj is Solid subSolid)
                    {
                        foreach (Face face in subSolid.Faces)
                        {
                            if (face is PlanarFace planar && planar.FaceNormal.Z > 0.5)
                            {
                                if (planar.Area > bestArea)
                                {
                                    bestArea = planar.Area;
                                    bestFace = planar;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (bestFace is null) return null;

        var loops = bestFace.GetEdgesAsCurveLoops();
        if (loops.Count == 0) return null;

        // Use the outermost loop (largest perimeter)
        var outerLoop = loops[0];
        var points = new List<XYZ>();
        foreach (var curve in outerLoop)
        {
            points.Add(curve.GetEndPoint(0));
        }
        return points;
    }
}
