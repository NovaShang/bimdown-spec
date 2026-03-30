using System.Collections.Generic;
using Autodesk.Revit.DB;
using BimDown.RevitAddin;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class ShortIdGeneratorTests : RevitApiTest
{
    [Test]
    public async Task GetOrAssign_GeneratesIncrementingShortIds()
    {
        var gen = new ShortIdGenerator();
        var id1 = gen.GetOrAssign("wall", "guid-1");
        var id2 = gen.GetOrAssign("wall", "guid-2");
        var id3 = gen.GetOrAssign("door", "guid-3");

        await Assert.That(id1).IsEqualTo("w-1");
        await Assert.That(id2).IsEqualTo("w-2");
        await Assert.That(id3).IsEqualTo("d-1");

        // Requesting same again returns the same
        var id1Again = gen.GetOrAssign("wall", "guid-1");
        await Assert.That(id1Again).IsEqualTo("w-1");
    }

    [Test]
    public async Task Mappings_AreCollectedProperly()
    {
        // This simulates the dictionary that will be exported to _IdMap.csv
        var gen = new ShortIdGenerator();
        gen.GetOrAssign("wall", "guid-1");
        gen.GetOrAssign("wall", "guid-2");

        var map = gen.Mappings;
        await Assert.That(map.Count).IsEqualTo(2);
        await Assert.That(map.ContainsKey("guid-1")).IsTrue();
        await Assert.That(map["guid-1"]).IsEqualTo("w-1");
        await Assert.That(map["guid-2"]).IsEqualTo("w-2");
    }

    [Test]
    public async Task GetOrAssign_NewPrefixes_FoundationRampRailingRoomSeparator()
    {
        var gen = new ShortIdGenerator();

        var f1 = gen.GetOrAssign("foundation", "guid-f1");
        var rp1 = gen.GetOrAssign("ramp", "guid-rp1");
        var rl1 = gen.GetOrAssign("railing", "guid-rl1");
        var rs1 = gen.GetOrAssign("room_separator", "guid-rs1");

        await Assert.That(f1).IsEqualTo("f-1");
        await Assert.That(rp1).IsEqualTo("rp-1");
        await Assert.That(rl1).IsEqualTo("rl-1");
        await Assert.That(rs1).IsEqualTo("rs-1");
    }

    [Test]
    public async Task RemapRows_ReplacesIdsAndReferences()
    {
        var gen = new ShortIdGenerator();
        
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "guid-1", ["level_id"] = "level-guid-1", ["host_id"] = null },
            new() { ["id"] = "guid-2", ["host_id"] = "guid-1" }
        };

        // Must have the levels already generated beforehand or during map
        gen.GetOrAssign("level", "level-guid-1");

        // Remap wall rows
        gen.RemapRows("wall", rows);

        await Assert.That(rows[0]["id"]).IsEqualTo("w-1");
        await Assert.That(rows[0]["level_id"]).IsEqualTo("lv-1");
        await Assert.That(rows[0]["host_id"]).IsNull();

        await Assert.That(rows[1]["id"]).IsEqualTo("w-2");
        await Assert.That(rows[1]["host_id"]).IsEqualTo("w-1");
    }
}
