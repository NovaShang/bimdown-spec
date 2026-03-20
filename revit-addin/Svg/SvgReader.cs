using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BimDown.RevitAddin.Svg;

static class SvgReader
{
    static readonly XNamespace Ns = "http://www.w3.org/2000/svg";

    public static Dictionary<string, Dictionary<string, string?>> ReadAll(string inputDir)
    {
        var result = new Dictionary<string, Dictionary<string, string?>>();

        if (!Directory.Exists(inputDir)) return result;

        foreach (var levelDir in Directory.EnumerateDirectories(inputDir))
        {
            foreach (var svgFile in Directory.EnumerateFiles(levelDir, "*.svg"))
            {
                var fileName = Path.GetFileName(svgFile);
                var mapping = SvgTableMapping.All.FirstOrDefault(m =>
                    string.Equals(m.SvgFileName, fileName, StringComparison.OrdinalIgnoreCase));
                if (mapping is null) continue;

                var doc = XDocument.Load(svgFile);
                var g = doc.Root?.Element(Ns + "g");
                if (g is null) continue;

                foreach (var el in g.Elements())
                {
                    var fields = mapping.RenderType switch
                    {
                        SvgRenderType.Line => ParseLine(el),
                        SvgRenderType.Point => ParsePoint(el),
                        SvgRenderType.Polygon => ParsePolygon(el),
                        SvgRenderType.Hosted => ParseHosted(el),
                        _ => null
                    };

                    if (fields is null) continue;
                    var id = el.Attribute("id")?.Value;
                    if (id is null) continue;

                    result[id] = fields;
                }
            }
        }

        return result;
    }

    static Dictionary<string, string?>? ParseLine(XElement el)
    {
        if (el.Name.LocalName != "line") return null;
        // Skip hosted elements (they have data-host)
        if (el.Attribute("data-host") is not null) return null;

        var x1 = el.Attribute("x1")?.Value;
        var y1 = el.Attribute("y1")?.Value;
        var x2 = el.Attribute("x2")?.Value;
        var y2 = el.Attribute("y2")?.Value;
        var sw = el.Attribute("stroke-width")?.Value;

        if (x1 is null || y1 is null || x2 is null || y2 is null) return null;

        return new Dictionary<string, string?>
        {
            ["start_x"] = x1,
            ["start_y"] = y1,
            ["end_x"] = x2,
            ["end_y"] = y2,
            ["thickness"] = sw,
        };
    }

    static Dictionary<string, string?>? ParsePoint(XElement el)
    {
        if (el.Name.LocalName == "circle")
        {
            var cx = el.Attribute("cx")?.Value;
            var cy = el.Attribute("cy")?.Value;
            var r = el.Attribute("r")?.Value;
            if (cx is null || cy is null || r is null) return null;

            var rVal = Parse(r);
            var diameter = Fmt(rVal * 2);
            return new Dictionary<string, string?>
            {
                ["x"] = cx,
                ["y"] = cy,
                ["shape"] = "round",
                ["size_x"] = diameter,
                ["size_y"] = diameter,
            };
        }

        if (el.Name.LocalName == "rect")
        {
            var x = el.Attribute("x")?.Value;
            var y = el.Attribute("y")?.Value;
            var w = el.Attribute("width")?.Value;
            var h = el.Attribute("height")?.Value;
            if (x is null || y is null || w is null || h is null) return null;

            var xVal = Parse(x);
            var yVal = Parse(y);
            var wVal = Parse(w);
            var hVal = Parse(h);

            var cx = xVal + wVal / 2;
            var cy = yVal + hVal / 2;

            var rotation = 0.0;
            var transform = el.Attribute("transform")?.Value;
            if (transform is not null)
            {
                var match = Regex.Match(transform, @"rotate\(([^,)]+)");
                if (match.Success)
                    rotation = Parse(match.Groups[1].Value);
            }

            var fields = new Dictionary<string, string?>
            {
                ["x"] = Fmt(cx),
                ["y"] = Fmt(cy),
                ["size_x"] = w,
                ["size_y"] = h,
            };

            if (rotation != 0)
                fields["rotation"] = Fmt(rotation);

            return fields;
        }

        return null;
    }

    static Dictionary<string, string?>? ParsePolygon(XElement el)
    {
        if (el.Name.LocalName != "polygon") return null;

        var pointsAttr = el.Attribute("points")?.Value;
        if (pointsAttr is null) return null;

        var json = SvgWriter.SvgPointsToJson(pointsAttr);
        if (json is null) return null;

        return new Dictionary<string, string?>
        {
            ["points"] = json,
        };
    }

    static Dictionary<string, string?>? ParseHosted(XElement el)
    {
        if (el.Name.LocalName != "line") return null;

        var hostId = el.Attribute("data-host")?.Value;
        if (hostId is null) return null;

        var x1 = el.Attribute("x1")?.Value;
        var y1 = el.Attribute("y1")?.Value;
        var x2 = el.Attribute("x2")?.Value;
        var y2 = el.Attribute("y2")?.Value;

        if (x1 is null || y1 is null || x2 is null || y2 is null) return null;

        return new Dictionary<string, string?>
        {
            ["host_id"] = hostId,
            ["svg_x1"] = x1,
            ["svg_y1"] = y1,
            ["svg_x2"] = x2,
            ["svg_y2"] = y2,
        };
    }

    /// <summary>
    /// Reverse-computes location_param for hosted elements given wall geometry.
    /// Call after ReadAll + CSV merge so wall data is available.
    /// </summary>
    public static void ResolveHostedParameters(
        Dictionary<string, Dictionary<string, string?>> svgFields,
        List<Dictionary<string, string?>> wallRows,
        List<Dictionary<string, string?>> structureWallRows)
    {
        var wallLookup = new Dictionary<string, SvgWriter.WallGeometry>();
        foreach (var rows in new[] { wallRows, structureWallRows })
        {
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
                wallLookup[id] = new SvgWriter.WallGeometry(
                    Parse(sx), Parse(sy), Parse(ex), Parse(ey), ParseOr(th, 0.2));
            }
        }

        foreach (var (_, fields) in svgFields)
        {
            if (!fields.ContainsKey("svg_x1")) continue;

            var hostId = fields.GetValueOrDefault("host_id");
            if (hostId is null || !wallLookup.TryGetValue(hostId, out var wall)) continue;

            var x1 = Parse(fields["svg_x1"]!);
            var y1 = Parse(fields["svg_y1"]!);
            var x2 = Parse(fields["svg_x2"]!);
            var y2 = Parse(fields["svg_y2"]!);

            // Opening midpoint
            var midX = (x1 + x2) / 2;
            var midY = (y1 + y2) / 2;

            // Project onto wall to get parameter
            var dx = wall.EndX - wall.StartX;
            var dy = wall.EndY - wall.StartY;
            var lenSq = dx * dx + dy * dy;
            var t = lenSq > 1e-10
                ? ((midX - wall.StartX) * dx + (midY - wall.StartY) * dy) / lenSq
                : 0;

            // Opening width
            var owDx = x2 - x1;
            var owDy = y2 - y1;
            var width = Math.Sqrt(owDx * owDx + owDy * owDy);

            fields["location_param"] = Fmt(t);
            fields["width"] = Fmt(width);

            // Clean up temporary SVG coordinate fields
            fields.Remove("svg_x1");
            fields.Remove("svg_y1");
            fields.Remove("svg_x2");
            fields.Remove("svg_y2");
        }
    }

    static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    static double ParseOr(string? s, double fallback) =>
        s is not null && double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    static string Fmt(double v) => UnitConverter.FormatDouble(v);
}
