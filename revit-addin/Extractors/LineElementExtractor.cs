using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class LineElementExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["start_x", "start_y", "end_x", "end_y", "length", "_svg_d"];
    public IReadOnlyList<string> ComputedFieldNames { get; } = ["start_x", "start_y", "end_x", "end_y", "length", "_svg_d"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        if (element.Location is not LocationCurve loc) return fields;

        switch (loc.Curve)
        {
            case Line line:
            {
                var start = line.GetEndPoint(0);
                var end = line.GetEndPoint(1);
                var sx = UnitConverter.Length(start.X);
                var sy = UnitConverter.Length(start.Y);
                var ex = UnitConverter.Length(end.X);
                var ey = UnitConverter.Length(end.Y);
                fields["start_x"] = UnitConverter.FormatDouble(sx);
                fields["start_y"] = UnitConverter.FormatDouble(sy);
                fields["end_x"] = UnitConverter.FormatDouble(ex);
                fields["end_y"] = UnitConverter.FormatDouble(ey);
                fields["length"] = UnitConverter.FormatDouble(UnitConverter.Length(line.Length));
                fields["_svg_d"] = $"M {Fmt(sx)},{Fmt(sy)} L {Fmt(ex)},{Fmt(ey)}";
                break;
            }
            case Arc arc:
            {
                var start = arc.GetEndPoint(0);
                var end = arc.GetEndPoint(1);
                var sx = UnitConverter.Length(start.X);
                var sy = UnitConverter.Length(start.Y);
                var ex = UnitConverter.Length(end.X);
                var ey = UnitConverter.Length(end.Y);
                fields["start_x"] = UnitConverter.FormatDouble(sx);
                fields["start_y"] = UnitConverter.FormatDouble(sy);
                fields["end_x"] = UnitConverter.FormatDouble(ex);
                fields["end_y"] = UnitConverter.FormatDouble(ey);
                fields["length"] = UnitConverter.FormatDouble(UnitConverter.Length(arc.Length));
                var r = UnitConverter.Length(arc.Radius);
                var (largeArc, sweep) = ComputeArcFlags(arc);
                fields["_svg_d"] = $"M {Fmt(sx)},{Fmt(sy)} A {Fmt(r)},{Fmt(r)} 0 {largeArc},{sweep} {Fmt(ex)},{Fmt(ey)}";
                break;
            }
        }

        return fields;
    }

    /// <summary>
    /// Computes SVG arc flags from a Revit Arc.
    /// large-arc-flag: 1 if sweep angle > π
    /// sweep-flag: 1 if clockwise in SVG coordinate space (Revit's positive Z normal = counterclockwise in math,
    /// but SVG Y is flipped, so positive Z normal → sweep=1 in SVG)
    /// </summary>
    internal static (int LargeArc, int Sweep) ComputeArcFlags(Arc arc)
    {
        // Sweep angle magnitude
        var startParam = arc.GetEndParameter(0);
        var endParam = arc.GetEndParameter(1);
        var sweepAngle = Math.Abs(endParam - startParam);
        var largeArc = sweepAngle > Math.PI ? 1 : 0;

        // Sweep direction: check arc normal Z component
        // Revit Arc with positive Z normal goes counterclockwise in XY plane.
        // SVG with scale(1,-1) flips Y, so CCW in Revit → CW in SVG → sweep=1
        var normal = arc.Normal;
        var sweep = normal.Z >= 0 ? 1 : 0;

        return (largeArc, sweep);
    }

    static string Fmt(double v) => UnitConverter.FormatDouble(v);
}
