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
    public async Task GetOrAssign_AllSpecPrefixes()
    {
        var gen = new ShortIdGenerator();
        var expected = new Dictionary<string, string>
        {
            ["level"] = "lv-1", ["grid"] = "gr-1", ["wall"] = "w-1", ["column"] = "c-1",
            ["slab"] = "sl-1", ["space"] = "sp-1", ["door"] = "d-1", ["window"] = "wn-1",
            ["opening"] = "op-1", ["stair"] = "st-1", ["ramp"] = "rp-1", ["railing"] = "rl-1",
            ["curtain_wall"] = "cw-1", ["ceiling"] = "cl-1", ["roof"] = "ro-1",
            ["room_separator"] = "rs-1",
            ["structure_wall"] = "sw-1", ["structure_column"] = "sc-1",
            ["structure_slab"] = "ss-1", ["beam"] = "bm-1", ["brace"] = "br-1",
            ["foundation"] = "f-1",
            ["duct"] = "du-1", ["pipe"] = "pi-1", ["cable_tray"] = "ct-1",
            ["conduit"] = "co-1", ["equipment"] = "eq-1", ["terminal"] = "tm-1",
            ["mep_node"] = "mn-1", ["mesh"] = "ms-1",
        };

        foreach (var (table, expectedId) in expected)
        {
            var id = gen.GetOrAssign(table, $"guid-{table}");
            await Assert.That(id).IsEqualTo(expectedId);
        }
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

        // Level is global
        gen.RemapGlobalRows("level", [new() { ["id"] = "level-guid-1" }]);

        // Remap wall rows scoped to lv-1
        gen.RemapPartitionedRows("wall", rows, _ => "lv-1");

        await Assert.That(rows[0]["id"]).IsEqualTo("w-1");
        await Assert.That(rows[0]["level_id"]).IsEqualTo("lv-1");
        await Assert.That(rows[0]["host_id"]).IsNull();

        await Assert.That(rows[1]["id"]).IsEqualTo("w-2");
        await Assert.That(rows[1]["host_id"]).IsEqualTo("w-1");
    }

    [Test]
    public async Task LevelScopedIds_IndependentCountersPerDirectory()
    {
        var gen = new ShortIdGenerator();

        var lv1Wall = gen.GetOrAssign("wall", "guid-a", "lv-1");
        var lv2Wall = gen.GetOrAssign("wall", "guid-b", "lv-2");
        var lv1Wall2 = gen.GetOrAssign("wall", "guid-c", "lv-1");

        // Each directory has independent counters
        await Assert.That(lv1Wall).IsEqualTo("w-1");
        await Assert.That(lv2Wall).IsEqualTo("w-1");
        await Assert.That(lv1Wall2).IsEqualTo("w-2");

        // Same uniqueId returns same result
        await Assert.That(gen.GetOrAssign("wall", "guid-a", "lv-1")).IsEqualTo("w-1");
    }
}
