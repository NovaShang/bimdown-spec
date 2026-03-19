using BimDown.RevitAddin;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class CsvRoundTripTests : RevitApiTest
{
    [Test]
    public async Task Write_Then_Read_PreservesData()
    {
        var columns = new List<string> { "id", "name", "value" };
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "abc-123", ["name"] = "Wall A", ["value"] = "3.14" },
            new() { ["id"] = "def-456", ["name"] = "Wall B", ["value"] = null },
        };

        var (readCols, readRows) = RevitTestHelper.RoundTripCsv(columns, rows);

        await Assert.That(readCols).IsEquivalentTo(columns);
        await Assert.That(readRows.Count).IsEqualTo(2);

        await Assert.That(readRows[0]["id"]).IsEqualTo("abc-123");
        await Assert.That(readRows[0]["name"]).IsEqualTo("Wall A");
        await Assert.That(readRows[0]["value"]).IsEqualTo("3.14");

        await Assert.That(readRows[1]["id"]).IsEqualTo("def-456");
        await Assert.That(readRows[1]["name"]).IsEqualTo("Wall B");
        await Assert.That(readRows[1]["value"]).IsNull();
    }

    [Test]
    public async Task Write_Then_Read_HandlesCommasInValues()
    {
        var columns = new List<string> { "id", "data" };
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "1", ["data"] = "hello, world" },
        };

        var (_, readRows) = RevitTestHelper.RoundTripCsv(columns, rows);
        await Assert.That(readRows[0]["data"]).IsEqualTo("hello, world");
    }

    [Test]
    public async Task Write_Then_Read_HandlesQuotesInValues()
    {
        var columns = new List<string> { "id", "data" };
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "1", ["data"] = "he said \"hello\"" },
        };

        var (_, readRows) = RevitTestHelper.RoundTripCsv(columns, rows);
        await Assert.That(readRows[0]["data"]).IsEqualTo("he said \"hello\"");
    }

    [Test]
    public async Task Write_Then_Read_EmptyRows()
    {
        var columns = new List<string> { "id", "name" };
        var rows = new List<Dictionary<string, string?>>();

        var (readCols, readRows) = RevitTestHelper.RoundTripCsv(columns, rows);
        await Assert.That(readCols).IsEquivalentTo(columns);
        await Assert.That(readRows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Write_Then_Read_PolygonJson()
    {
        var columns = new List<string> { "id", "points" };
        var json = "[[1.5,2.5],[3.5,4.5],[5.5,6.5]]";
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["id"] = "1", ["points"] = json },
        };

        var (_, readRows) = RevitTestHelper.RoundTripCsv(columns, rows);
        await Assert.That(readRows[0]["points"]).IsEqualTo(json);
    }
}
