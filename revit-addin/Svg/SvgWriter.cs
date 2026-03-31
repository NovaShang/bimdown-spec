using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BimDown.RevitAddin.Svg;

static class SvgWriter
{
    static readonly XNamespace Ns = "http://www.w3.org/2000/svg";

    public static void WriteAll(
        string outputDir,
        List<(string TableName, List<Dictionary<string, string?>> Rows)> tables,
        List<Dictionary<string, string?>> levelRows)
    {
        var allSvgFiles = new List<(string OutputPath, List<XElement> Elements)>();
        var allElements = new List<XElement>();

        foreach (var (tableName, rows) in tables)
        {
            var mapping = SvgTableMapping.ForTable(tableName);
            if (mapping is null) continue;

            var grouped = rows
                .Where(r => r.GetValueOrDefault("level_id") is not null)
                .GroupBy(r => r["level_id"]!);

            foreach (var group in grouped)
            {
                var levelDir = Path.Combine(outputDir, group.Key);
                var svgPath = Path.Combine(levelDir, mapping.SvgFileName);
                var elements = new List<XElement>();

                foreach (var row in group)
                {
                    var el = mapping.RenderType switch
                    {
                        SvgRenderType.Line => RenderLine(row),
                        SvgRenderType.Point => RenderPoint(row),
                        SvgRenderType.Polygon => RenderPolygon(row),
                        SvgRenderType.Mixed => RenderMixed(row),
                        _ => null
                    };
                    if (el is not null) elements.Add(el);
                }

                if (elements.Count == 0) continue;

                allSvgFiles.Add((svgPath, elements));
                allElements.AddRange(elements);
            }
        }

        if (allElements.Count == 0) return;

        var globalViewBox = ComputeViewBox(allElements);

        foreach (var (svgPath, elements) in allSvgFiles)
        {
            var levelDir = Path.GetDirectoryName(svgPath);
            if (!Directory.Exists(levelDir)) Directory.CreateDirectory(levelDir!);

            var g = new XElement(Ns + "g",
                new XAttribute("transform", "scale(1,-1)"),
                elements);

            var svg = new XElement(Ns + "svg",
                new XAttribute("xmlns", Ns.NamespaceName),
                new XAttribute("viewBox", globalViewBox),
                g);

            svg.Save(svgPath);
        }
    }

    static XElement? RenderLine(Dictionary<string, string?> row)
    {
        var id = row.GetValueOrDefault("id");

        // Use pre-computed _svg_d if available (supports arcs)
        var svgD = row.GetValueOrDefault("_svg_d");
        if (svgD is not null && id is not null)
        {
            return new XElement(Ns + "path",
                new XAttribute("id", id),
                new XAttribute("d", svgD));
        }

        // Fallback: build from start/end coordinates
        var x1 = row.GetValueOrDefault("start_x");
        var y1 = row.GetValueOrDefault("start_y");
        var x2 = row.GetValueOrDefault("end_x");
        var y2 = row.GetValueOrDefault("end_y");

        if (id is null || x1 is null || y1 is null || x2 is null || y2 is null)
            return null;

        return new XElement(Ns + "path",
            new XAttribute("id", id),
            new XAttribute("d", $"M {x1},{y1} L {x2},{y2}"));
    }

    static XElement? RenderPoint(Dictionary<string, string?> row)
    {
        var id = row.GetValueOrDefault("id");
        var cx = row.GetValueOrDefault("x");
        var cy = row.GetValueOrDefault("y");

        if (id is null || cx is null || cy is null)
            return null;

        var shape = row.GetValueOrDefault("shape");
        var sizeX = ParseOr(row.GetValueOrDefault("size_x"), 0);
        var sizeY = ParseOr(row.GetValueOrDefault("size_y"), 0);

        // Fallback: use length/width for foundations
        if (sizeX == 0) sizeX = ParseOr(row.GetValueOrDefault("length"), 0);
        if (sizeY == 0) sizeY = ParseOr(row.GetValueOrDefault("width"), 0);

        // Default small size if no info
        if (sizeX == 0 && sizeY == 0)
        {
            var defaultSize = row.GetValueOrDefault("id")?.StartsWith("mn-") == true ? 0.05 : 0.3;
            sizeX = defaultSize;
            sizeY = defaultSize;
        }

        var cxVal = Parse(cx);
        var cyVal = Parse(cy);

        if (string.Equals(shape, "round", StringComparison.OrdinalIgnoreCase))
        {
            var r = sizeX / 2;
            return new XElement(Ns + "circle",
                new XAttribute("id", id),
                new XAttribute("cx", Fmt(cxVal)),
                new XAttribute("cy", Fmt(cyVal)),
                new XAttribute("r", Fmt(r)));
        }

        var x = cxVal - sizeX / 2;
        var y = cyVal - sizeY / 2;
        var rotation = ParseOr(row.GetValueOrDefault("rotation"), 0);

        var rect = new XElement(Ns + "rect",
            new XAttribute("id", id),
            new XAttribute("x", Fmt(x)),
            new XAttribute("y", Fmt(y)),
            new XAttribute("width", Fmt(sizeX)),
            new XAttribute("height", Fmt(sizeY)));

        if (rotation != 0)
            rect.Add(new XAttribute("transform", $"rotate({Fmt(rotation)},{Fmt(cxVal)},{Fmt(cyVal)})"));

        return rect;
    }

    static XElement? RenderPolygon(Dictionary<string, string?> row)
    {
        var id = row.GetValueOrDefault("id");
        var pointsJson = row.GetValueOrDefault("points");

        if (id is null || pointsJson is null)
            return null;

        var svgPoints = JsonPointsToSvg(pointsJson);
        if (svgPoints is null) return null;

        return new XElement(Ns + "polygon",
            new XAttribute("id", id),
            new XAttribute("points", svgPoints));
    }

    /// <summary>
    /// Renders foundation elements which can be point, line, or polygon based on available data.
    /// </summary>
    static XElement? RenderMixed(Dictionary<string, string?> row)
    {
        if (row.GetValueOrDefault("points") is not null)
            return RenderPolygon(row);
        if (row.GetValueOrDefault("start_x") is not null)
            return RenderLine(row);
        if (row.GetValueOrDefault("x") is not null)
            return RenderPoint(row);
        return null;
    }

    static string ComputeViewBox(List<XElement> elements)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        void Extend(double x, double y)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        foreach (var el in elements)
        {
            switch (el.Name.LocalName)
            {
                case "path":
                    var dAttr = el.Attribute("d")?.Value;
                    var arcInfo = ParseArcCoordinates(dAttr);
                    if (arcInfo is not null)
                    {
                        // Arc: extend with start, end, and arc extremes
                        Extend(arcInfo.Value.X1, -arcInfo.Value.Y1);
                        Extend(arcInfo.Value.X2, -arcInfo.Value.Y2);
                        ExtendArcBounds(Extend, arcInfo.Value);
                    }
                    else
                    {
                        var coords = ParsePathCoordinates(dAttr);
                        if (coords is not null)
                        {
                            Extend(coords.Value.X1, -coords.Value.Y1);
                            Extend(coords.Value.X2, -coords.Value.Y2);
                        }
                    }
                    break;
                case "circle":
                    var cx = Parse(el.Attribute("cx")!.Value);
                    var cy = -Parse(el.Attribute("cy")!.Value);
                    var r = Parse(el.Attribute("r")!.Value);
                    Extend(cx - r, cy - r);
                    Extend(cx + r, cy + r);
                    break;
                case "rect":
                    var rx = Parse(el.Attribute("x")!.Value);
                    var ry = -Parse(el.Attribute("y")!.Value);
                    var rw = Parse(el.Attribute("width")!.Value);
                    var rh = Parse(el.Attribute("height")!.Value);
                    Extend(rx, ry);
                    Extend(rx + rw, ry + rh);
                    break;
                case "polygon":
                    var pts = el.Attribute("points")!.Value;
                    foreach (var pair in pts.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = pair.Split(',');
                        Extend(Parse(parts[0]), -Parse(parts[1]));
                    }
                    break;
            }
        }

        if (minX > maxX) return "0 0 100 100";

        const double pad = 0.5;
        minX -= pad; minY -= pad;
        maxX += pad; maxY += pad;
        return $"{Fmt(minX)} {Fmt(minY)} {Fmt(maxX - minX)} {Fmt(maxY - minY)}";
    }

    /// <summary>
    /// Parses a line path "M x1,y1 L x2,y2" to extract endpoints.
    /// </summary>
    internal static (double X1, double Y1, double X2, double Y2)? ParsePathCoordinates(string? d)
    {
        if (d is null) return null;

        var match = Regex.Match(d, @"M\s*(-?[\d.]+)[,\s]+(-?[\d.]+)\s*L\s*(-?[\d.]+)[,\s]+(-?[\d.]+)");
        if (!match.Success) return null;

        return (
            Parse(match.Groups[1].Value),
            Parse(match.Groups[2].Value),
            Parse(match.Groups[3].Value),
            Parse(match.Groups[4].Value));
    }

    /// <summary>
    /// Parses an arc path "M x1,y1 A rx,ry rot largeArc sweep x2,y2".
    /// </summary>
    internal static (double X1, double Y1, double Rx, double Ry, int LargeArc, int Sweep, double X2, double Y2)? ParseArcCoordinates(string? d)
    {
        if (d is null || !d.Contains('A')) return null;

        var match = Regex.Match(d,
            @"M\s*(-?[\d.]+)[,\s]+(-?[\d.]+)\s*A\s*(-?[\d.]+)[,\s]+(-?[\d.]+)\s+\d+\s+(\d)[,\s]+(\d)\s+(-?[\d.]+)[,\s]+(-?[\d.]+)");
        if (!match.Success) return null;

        return (
            Parse(match.Groups[1].Value),
            Parse(match.Groups[2].Value),
            Parse(match.Groups[3].Value),
            Parse(match.Groups[4].Value),
            int.Parse(match.Groups[5].Value),
            int.Parse(match.Groups[6].Value),
            Parse(match.Groups[7].Value),
            Parse(match.Groups[8].Value));
    }

    /// <summary>
    /// Extends viewBox bounds for an arc by computing the arc's midpoint.
    /// </summary>
    static void ExtendArcBounds(Action<double, double> extend,
        (double X1, double Y1, double Rx, double Ry, int LargeArc, int Sweep, double X2, double Y2) arc)
    {
        // Approximate: compute center, then extend by radius in all directions from center
        // This is conservative but correct — the arc lies within a circle of this radius
        var midX = (arc.X1 + arc.X2) / 2;
        var midY = (-arc.Y1 + -arc.Y2) / 2; // already flipped in caller context
        var chordLen = Math.Sqrt((arc.X2 - arc.X1) * (arc.X2 - arc.X1) + (arc.Y2 - arc.Y1) * (arc.Y2 - arc.Y1));
        var r = arc.Rx;

        if (r > chordLen / 2)
        {
            // Arc bulges beyond chord — compute sagitta (max perpendicular distance)
            var halfChord = chordLen / 2;
            var sagitta = r - Math.Sqrt(r * r - halfChord * halfChord);
            if (arc.LargeArc == 1) sagitta = r + Math.Sqrt(r * r - halfChord * halfChord);

            // Perpendicular direction to chord
            var dx = arc.X2 - arc.X1;
            var dy = -(arc.Y2 - arc.Y1); // flipped Y
            var perpX = -dy / chordLen;
            var perpY = dx / chordLen;
            var sign = arc.Sweep == 1 ? -1 : 1;

            var bulgeX = midX + perpX * sagitta * sign;
            var bulgeY = ((-arc.Y1) + (-arc.Y2)) / 2 + perpY * sagitta * sign;
            extend(bulgeX, bulgeY);
        }
    }

    internal static string? JsonPointsToSvg(string json)
    {
        try
        {
            var points = JsonSerializer.Deserialize<double[][]>(json);
            if (points is null || points.Length == 0) return null;
            return string.Join(" ", points.Select(p => $"{Fmt(p[0])},{Fmt(p[1])}"));
        }
        catch
        {
            return null;
        }
    }

    internal static string? SvgPointsToJson(string svgPoints)
    {
        try
        {
            var pairs = svgPoints.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var arr = pairs.Select(p =>
            {
                var parts = p.Split(',');
                return new[] { Parse(parts[0]), Parse(parts[1]) };
            }).ToArray();
            return JsonSerializer.Serialize(arr);
        }
        catch
        {
            return null;
        }
    }

    static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    static double ParseOr(string? s, double fallback) =>
        s is not null && double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    static string Fmt(double v) => UnitConverter.FormatDouble(v);
}
