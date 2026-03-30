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
                    var id = el.Attribute("id")?.Value;
                    if (id is null) continue;

                    var fields = mapping.RenderType switch
                    {
                        SvgRenderType.Line => ParsePath(el),
                        SvgRenderType.Point => ParsePoint(el),
                        SvgRenderType.Polygon => ParsePolygon(el),
                        SvgRenderType.Mixed => ParseMixed(el),
                        _ => null
                    };

                    if (fields is null) continue;
                    result[id] = fields;
                }
            }
        }

        return result;
    }

    static Dictionary<string, string?>? ParsePath(XElement el)
    {
        if (el.Name.LocalName != "path") return null;

        var d = el.Attribute("d")?.Value;
        var coords = SvgWriter.ParsePathCoordinates(d);
        if (coords is null) return null;

        return new Dictionary<string, string?>
        {
            ["start_x"] = Fmt(coords.Value.X1),
            ["start_y"] = Fmt(coords.Value.Y1),
            ["end_x"] = Fmt(coords.Value.X2),
            ["end_y"] = Fmt(coords.Value.Y2),
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

    /// <summary>
    /// Parses mixed-geometry SVG elements (for foundation table).
    /// Dispatches based on SVG element type.
    /// </summary>
    static Dictionary<string, string?>? ParseMixed(XElement el) =>
        el.Name.LocalName switch
        {
            "path" => ParsePath(el),
            "polygon" => ParsePolygon(el),
            "rect" or "circle" => ParsePoint(el),
            _ => null
        };

    static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    static string Fmt(double v) => UnitConverter.FormatDouble(v);
}
