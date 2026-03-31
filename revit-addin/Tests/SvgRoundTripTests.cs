using BimDown.RevitAddin.Svg;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class SvgRoundTripTests : RevitApiTest
{
    [Test]
    public async Task LineRoundTrip_PreservesCoordinatesAndThickness()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                Row("w-1", "lv-1", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "5"), ("end_y", "0"), ("thickness", "0.2")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);
            var read = SvgReader.ReadAll(outputDir);

            await Assert.That(read.ContainsKey("w-1")).IsTrue();
            var fields = read["w-1"];
            await Assert.That(fields["start_x"]).IsEqualTo("0");
            await Assert.That(fields["start_y"]).IsEqualTo("0");
            await Assert.That(fields["end_x"]).IsEqualTo("5");
            await Assert.That(fields["end_y"]).IsEqualTo("0");
            // thickness is no longer stored in SVG (it's a CSV-only field)
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task PointRoundTrip_RectPreservesCenterAndSize()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var colRows = new List<Dictionary<string, string?>>
            {
                Row("c-1", "lv-1", ("x", "2"), ("y", "3"),
                    ("size_x", "0.4"), ("size_y", "0.6"), ("shape", null), ("rotation", "0")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("column", colRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);
            var read = SvgReader.ReadAll(outputDir);

            await Assert.That(read.ContainsKey("c-1")).IsTrue();
            var fields = read["c-1"];
            await Assert.That(fields["x"]).IsEqualTo("2");
            await Assert.That(fields["y"]).IsEqualTo("3");
            await Assert.That(fields["size_x"]).IsEqualTo("0.4");
            await Assert.That(fields["size_y"]).IsEqualTo("0.6");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task PointRoundTrip_CirclePreservesRadiusAndShape()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var colRows = new List<Dictionary<string, string?>>
            {
                Row("c-2", "lv-1", ("x", "5"), ("y", "5"),
                    ("size_x", "0.6"), ("size_y", "0.6"), ("shape", "round"), ("rotation", "0")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("column", colRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);
            var read = SvgReader.ReadAll(outputDir);

            await Assert.That(read.ContainsKey("c-2")).IsTrue();
            var fields = read["c-2"];
            await Assert.That(fields["x"]).IsEqualTo("5");
            await Assert.That(fields["y"]).IsEqualTo("5");
            await Assert.That(fields["shape"]).IsEqualTo("round");
            await Assert.That(fields["size_x"]).IsEqualTo("0.6");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task PolygonRoundTrip_PreservesPoints()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var slabRows = new List<Dictionary<string, string?>>
            {
                Row("sl-1", "lv-1", ("points", "[[0,0],[5,0],[5,5],[0,5]]")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("slab", slabRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);
            var read = SvgReader.ReadAll(outputDir);

            await Assert.That(read.ContainsKey("sl-1")).IsTrue();
            var fields = read["sl-1"];
            await Assert.That(fields["points"]).IsEqualTo("[[0,0],[5,0],[5,5],[0,5]]");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task DoorWindowSpace_NoSvgGenerated()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                Row("w-1", "lv-1", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "10"), ("end_y", "0"), ("thickness", "0.2")),
            };

            var doorRows = new List<Dictionary<string, string?>>
            {
                Row("d-1", "lv-1", ("host_id", "w-1"),
                    ("position", "0.5"), ("width", "1")),
            };

            var spaceRows = new List<Dictionary<string, string?>>
            {
                Row("sp-1", "lv-1", ("x", "2"), ("y", "3"), ("name", "Room")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
                ("door", doorRows),
                ("space", spaceRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);

            // Wall SVG should exist
            await Assert.That(File.Exists(Path.Combine(outputDir, "lv-1", "wall.svg"))).IsTrue();
            // Door, window, space SVG should NOT exist
            await Assert.That(File.Exists(Path.Combine(outputDir, "lv-1", "door.svg"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, "lv-1", "window.svg"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, "lv-1", "space.svg"))).IsFalse();
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task DirectoryStructure_GroupsByLevel()
    {
        var (outputDir, _) = SetupTempDir();
        var levelRows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "lv-1", ["name"] = "Level 1" },
            new() { ["id"] = "lv-2", ["name"] = "Level 2" },
        };

        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                Row("w-1", "lv-1", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "5"), ("end_y", "0"), ("thickness", "0.2")),
                Row("w-2", "lv-2", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "3"), ("end_y", "0"), ("thickness", "0.15")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);

            await Assert.That(File.Exists(Path.Combine(outputDir, "lv-1", "wall.svg"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "lv-2", "wall.svg"))).IsTrue();
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task SvgStructure_HasCorrectTransformAndNamespace()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                Row("w-1", "lv-1", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "5"), ("end_y", "0"), ("thickness", "0.2")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);

            var svgPath = Path.Combine(outputDir, "lv-1", "wall.svg");
            var content = File.ReadAllText(svgPath);

            await Assert.That(content).Contains("xmlns=\"http://www.w3.org/2000/svg\"");
            await Assert.That(content).Contains("scale(1,-1)");
            await Assert.That(content).Contains("<path");
            await Assert.That(content).DoesNotContain("<line");
            await Assert.That(content).DoesNotContain("<defs");
            await Assert.That(content).DoesNotContain("<script");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task MixedRoundTrip_FoundationPointLinePolygon()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var foundationRows = new List<Dictionary<string, string?>>
            {
                // Isolated (point)
                Row("f-1", "lv-1", ("x", "5"), ("y", "5"),
                    ("size_x", "1.2"), ("size_y", "1.2"), ("shape", null)),
                // Strip (line)
                Row("f-2", "lv-1", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "10"), ("end_y", "0")),
                // Raft (polygon)
                Row("f-3", "lv-1", ("points", "[[0,0],[8,0],[8,6],[0,6]]")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("foundation", foundationRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);
            var read = SvgReader.ReadAll(outputDir);

            // Isolated foundation → <rect>
            await Assert.That(read.ContainsKey("f-1")).IsTrue();
            await Assert.That(read["f-1"]["x"]).IsEqualTo("5");
            await Assert.That(read["f-1"]["y"]).IsEqualTo("5");

            // Strip foundation → <path>
            await Assert.That(read.ContainsKey("f-2")).IsTrue();
            await Assert.That(read["f-2"]["start_x"]).IsEqualTo("0");
            await Assert.That(read["f-2"]["end_x"]).IsEqualTo("10");

            // Raft foundation → <polygon>
            await Assert.That(read.ContainsKey("f-3")).IsTrue();
            await Assert.That(read["f-3"]["points"]).IsEqualTo("[[0,0],[8,0],[8,6],[0,6]]");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task PathFormat_RendersCorrectDAttribute()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                Row("w-1", "lv-1", ("start_x", "1.5"), ("start_y", "2.3"),
                    ("end_x", "7.8"), ("end_y", "4.1")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);

            var content = File.ReadAllText(Path.Combine(outputDir, "lv-1", "wall.svg"));
            await Assert.That(content).Contains("d=\"M 1.5,2.3 L 7.8,4.1\"");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task NoStyling_SvgHasNoFillOrStroke()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                Row("w-1", "lv-1", ("start_x", "0"), ("start_y", "0"),
                    ("end_x", "5"), ("end_y", "0")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);

            var content = File.ReadAllText(Path.Combine(outputDir, "lv-1", "wall.svg"));
            await Assert.That(content).DoesNotContain("stroke=");
            await Assert.That(content).DoesNotContain("fill=");
            await Assert.That(content).DoesNotContain("stroke-width=");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task MixedRoundTrip_OpeningRectAndPolygon()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var openingRows = new List<Dictionary<string, string?>>
            {
                // Slab opening as rect (point-like)
                Row("op-1", "lv-1", ("x", "3"), ("y", "3"),
                    ("size_x", "2"), ("size_y", "1.5"), ("host_id", "sl-1")),
                // Slab opening as polygon
                Row("op-2", "lv-1", ("points", "[[3,3],[5,3],[5,4.5],[3,4.5]]"),
                    ("host_id", "sl-1")),
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("opening", openingRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);
            var read = SvgReader.ReadAll(outputDir);

            // Rect opening
            await Assert.That(read.ContainsKey("op-1")).IsTrue();
            await Assert.That(read["op-1"]["x"]).IsEqualTo("3");

            // Polygon opening
            await Assert.That(read.ContainsKey("op-2")).IsTrue();
            await Assert.That(read["op-2"]["points"]).IsEqualTo("[[3,3],[5,3],[5,4.5],[3,4.5]]");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task ArcPathRoundTrip_PreservesArcGeometry()
    {
        var (outputDir, levelRows) = SetupTempDir();
        try
        {
            var wallRows = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["id"] = "w-1",
                    ["level_id"] = "lv-1",
                    ["start_x"] = "0",
                    ["start_y"] = "0",
                    ["end_x"] = "5",
                    ["end_y"] = "0",
                    ["_svg_d"] = "M 0,0 A 3,3 0 0,1 5,0",
                },
            };

            var tables = new List<(string, List<Dictionary<string, string?>>)>
            {
                ("wall", wallRows),
            };

            SvgWriter.WriteAll(outputDir, tables, levelRows);

            // Verify SVG contains arc path
            var content = File.ReadAllText(Path.Combine(outputDir, "lv-1", "wall.svg"));
            await Assert.That(content).Contains("d=\"M 0,0 A 3,3 0 0,1 5,0\"");

            // Read back and verify coordinates + _svg_d preserved
            var read = SvgReader.ReadAll(outputDir);
            await Assert.That(read.ContainsKey("w-1")).IsTrue();
            var fields = read["w-1"];
            await Assert.That(fields["start_x"]).IsEqualTo("0");
            await Assert.That(fields["start_y"]).IsEqualTo("0");
            await Assert.That(fields["end_x"]).IsEqualTo("5");
            await Assert.That(fields["end_y"]).IsEqualTo("0");
            await Assert.That(fields["_svg_d"]).IsEqualTo("M 0,0 A 3,3 0 0,1 5,0");
        }
        finally { Cleanup(outputDir); }
    }

    [Test]
    public async Task ParseArcCoordinates_ParsesCorrectly()
    {
        var result = SvgWriter.ParseArcCoordinates("M 1.5,2.3 A 4,4 0 1,0 7.8,4.1");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.X1).IsEqualTo(1.5);
        await Assert.That(result!.Value.Y1).IsEqualTo(2.3);
        await Assert.That(result!.Value.Rx).IsEqualTo(4);
        await Assert.That(result!.Value.LargeArc).IsEqualTo(1);
        await Assert.That(result!.Value.Sweep).IsEqualTo(0);
        await Assert.That(result!.Value.X2).IsEqualTo(7.8);
        await Assert.That(result!.Value.Y2).IsEqualTo(4.1);
    }

    [Test]
    public async Task ParseArcCoordinates_ReturnsNullForLine()
    {
        var result = SvgWriter.ParseArcCoordinates("M 0,0 L 5,5");
        await Assert.That(result).IsNull();
    }

    static (string OutputDir, List<Dictionary<string, string?>> LevelRows) SetupTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"BimDown_SvgTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var levelRows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "lv-1", ["name"] = "L1" },
        };
        return (dir, levelRows);
    }

    static Dictionary<string, string?> Row(string id, string levelId, params (string Key, string? Value)[] fields)
    {
        var row = new Dictionary<string, string?>
        {
            ["id"] = id,
            ["level_id"] = levelId,
        };
        foreach (var (key, value) in fields)
            row[key] = value;
        return row;
    }

    static void AssertClose(double expected, double actual, double tolerance = 1e-6)
    {
        var diff = Math.Abs(expected - actual);
        if (diff > tolerance)
            throw new Exception($"Expected {expected} but got {actual} (diff={diff})");
    }

    static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }
}
