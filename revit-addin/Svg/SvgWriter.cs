using System.Globalization;
using System.Text.Json;
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
        var wallLookup = BuildWallLookup(tables);
        var mepNodeLookup = BuildMepNodeLookup(tables);
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
                        SvgRenderType.Line => RenderLine(row, tableName, mepNodeLookup),
                        SvgRenderType.Point => RenderPoint(row, tableName),
                        SvgRenderType.Polygon => RenderPolygon(row, tableName),
                        SvgRenderType.Hosted => RenderHosted(row, wallLookup),
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

    static XElement? RenderLine(Dictionary<string, string?> row, string tableName, Dictionary<string, (double X, double Y)> nodeLookup)
    {
        var id = row.GetValueOrDefault("id");
        var x1 = row.GetValueOrDefault("start_x");
        var y1 = row.GetValueOrDefault("start_y");
        var x2 = row.GetValueOrDefault("end_x");
        var y2 = row.GetValueOrDefault("end_y");

        if (id is null || x1 is null || y1 is null || x2 is null || y2 is null)
            return null;

        var strokeWidth = tableName switch
        {
            "wall" or "structure_wall" => row.GetValueOrDefault("thickness"),
            "beam" or "brace" => row.GetValueOrDefault("size_y"),
            "stair" => row.GetValueOrDefault("width"),
            "strip_foundation" => row.GetValueOrDefault("width"),
            "duct" or "pipe" or "cable_tray" or "conduit" => row.GetValueOrDefault("size_x"),
            _ => row.GetValueOrDefault("thickness")
        };

        strokeWidth ??= "0.1";

        var mainLine = new XElement(Ns + "line",
            new XAttribute("id", id),
            new XAttribute("x1", x1),
            new XAttribute("y1", y1),
            new XAttribute("x2", x2),
            new XAttribute("y2", y2),
            new XAttribute("stroke", "black"),
            new XAttribute("stroke-width", strokeWidth),
            new XAttribute("stroke-linecap", "square"));

        var isMepCurve = tableName is "duct" or "pipe" or "cable_tray" or "conduit";
        if (!isMepCurve) return mainLine;

        var startNodeId = row.GetValueOrDefault("start_node_id");
        var endNodeId = row.GetValueOrDefault("end_node_id");
        var extraLines = new List<XElement>();

        if (startNodeId != null && nodeLookup.TryGetValue(startNodeId, out var startP))
        {
            extraLines.Add(new XElement(Ns + "line",
                new XAttribute("x1", x1), new XAttribute("y1", y1),
                new XAttribute("x2", Fmt(startP.X)), new XAttribute("y2", Fmt(startP.Y)),
                new XAttribute("stroke", "black"), new XAttribute("stroke-width", strokeWidth)));
        }
        if (endNodeId != null && nodeLookup.TryGetValue(endNodeId, out var endP))
        {
            extraLines.Add(new XElement(Ns + "line",
                new XAttribute("x1", x2), new XAttribute("y1", y2),
                new XAttribute("x2", Fmt(endP.X)), new XAttribute("y2", Fmt(endP.Y)),
                new XAttribute("stroke", "black"), new XAttribute("stroke-width", strokeWidth)));
        }

        if (extraLines.Count == 0) return mainLine;

        extraLines.Add(mainLine);
        return new XElement(Ns + "g", new XAttribute("id", id + "_group"), extraLines);
    }

    static XElement? RenderPoint(Dictionary<string, string?> row, string tableName)
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

        // Default to small square if no size info
        if (sizeX == 0 && sizeY == 0) 
        { 
            // Make mep_node a very small dot (0.05) instead of a large 0.3 block
            var defaultSize = tableName == "mep_node" ? 0.05 : 0.3;
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
                new XAttribute("r", Fmt(r)),
                new XAttribute("fill", "black"));
        }

        var x = cxVal - sizeX / 2;
        var y = cyVal - sizeY / 2;
        var rotation = ParseOr(row.GetValueOrDefault("rotation"), 0);

        var rect = new XElement(Ns + "rect",
            new XAttribute("id", id),
            new XAttribute("x", Fmt(x)),
            new XAttribute("y", Fmt(y)),
            new XAttribute("width", Fmt(sizeX)),
            new XAttribute("height", Fmt(sizeY)),
            new XAttribute("fill", "black"));

        if (rotation != 0)
            rect.Add(new XAttribute("transform", $"rotate({Fmt(rotation)},{Fmt(cxVal)},{Fmt(cyVal)})"));

        return rect;
    }

    static XElement? RenderPolygon(Dictionary<string, string?> row, string tableName)
    {
        var id = row.GetValueOrDefault("id");
        var pointsJson = row.GetValueOrDefault("points");

        if (id is null || pointsJson is null)
            return null;

        var svgPoints = JsonPointsToSvg(pointsJson);
        if (svgPoints is null) return null;

        var (fill, stroke) = tableName == "space"
            ? ("rgba(0,0,255,0.1)", "blue")
            : ("rgba(128,128,128,0.2)", "gray");

        return new XElement(Ns + "polygon",
            new XAttribute("id", id),
            new XAttribute("points", svgPoints),
            new XAttribute("fill", fill),
            new XAttribute("stroke", stroke),
            new XAttribute("stroke-width", "0.05"));
    }

    static XElement? RenderHosted(Dictionary<string, string?> row,
        Dictionary<string, WallGeometry> wallLookup)
    {
        var id = row.GetValueOrDefault("id");
        var hostId = row.GetValueOrDefault("host_id");
        var locParam = row.GetValueOrDefault("location_param");
        var width = row.GetValueOrDefault("width");

        if (id is null || hostId is null || locParam is null)
            return null;

        if (!wallLookup.TryGetValue(hostId, out var wall))
            return null;

        var t = Parse(locParam);
        var w = ParseOr(width, 0.9); // default opening width

        // Compute center point along wall
        var midX = wall.StartX + t * (wall.EndX - wall.StartX);
        var midY = wall.StartY + t * (wall.EndY - wall.StartY);

        // Wall direction unit vector
        var dx = wall.EndX - wall.StartX;
        var dy = wall.EndY - wall.StartY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-10) return null;
        var ux = dx / len;
        var uy = dy / len;

        // Opening endpoints
        var halfW = w / 2;
        var x1 = midX - halfW * ux;
        var y1 = midY - halfW * uy;
        var x2 = midX + halfW * ux;
        var y2 = midY + halfW * uy;

        var strokeWidth = wall.Thickness + 0.02;

        return new XElement(Ns + "line",
            new XAttribute("id", id),
            new XAttribute("data-host", hostId),
            new XAttribute("x1", Fmt(x1)),
            new XAttribute("y1", Fmt(y1)),
            new XAttribute("x2", Fmt(x2)),
            new XAttribute("y2", Fmt(y2)),
            new XAttribute("stroke", "white"),
            new XAttribute("stroke-width", Fmt(strokeWidth)));
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
                case "line":
                    Extend(Parse(el.Attribute("x1")!.Value), -Parse(el.Attribute("y1")!.Value));
                    Extend(Parse(el.Attribute("x2")!.Value), -Parse(el.Attribute("y2")!.Value));
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
                    Extend(rx, ry - rh);
                    Extend(rx + rw, ry);
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

    static Dictionary<string, WallGeometry> BuildWallLookup(
        List<(string TableName, List<Dictionary<string, string?>> Rows)> tables)
    {
        var lookup = new Dictionary<string, WallGeometry>();
        foreach (var (tableName, rows) in tables)
        {
            if (tableName is not ("wall" or "structure_wall")) continue;
            foreach (var row in rows)
            {
                var id = row.GetValueOrDefault("id");
                if (id is null) continue;
                var sx = row.GetValueOrDefault("start_x");
                var sy = row.GetValueOrDefault("start_y");
                var ex = row.GetValueOrDefault("end_x");
                var ey = row.GetValueOrDefault("end_y");
                var th = row.GetValueOrDefault("thickness");
                if (sx is null || sy is null || ex is null || ey is null) continue;
                lookup[id] = new WallGeometry(Parse(sx), Parse(sy), Parse(ex), Parse(ey), ParseOr(th, 0.2));
            }
        }
        return lookup;
    }

    static Dictionary<string, (double X, double Y)> BuildMepNodeLookup(
        List<(string TableName, List<Dictionary<string, string?>> Rows)> tables)
    {
        var lookup = new Dictionary<string, (double X, double Y)>();
        foreach (var (tableName, rows) in tables)
        {
            if (tableName is not ("mep_node" or "equipment" or "terminal")) continue;
            foreach (var row in rows)
            {
                var id = row.GetValueOrDefault("id");
                var x = row.GetValueOrDefault("x");
                var y = row.GetValueOrDefault("y");
                if (id is null || x is null || y is null) continue;
                lookup[id] = (Parse(x), Parse(y));
            }
        }
        return lookup;
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

    static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }

    static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    static double ParseOr(string? s, double fallback) =>
        s is not null && double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    static string Fmt(double v) => UnitConverter.FormatDouble(v);

    internal record WallGeometry(double StartX, double StartY, double EndX, double EndY, double Thickness);
}
