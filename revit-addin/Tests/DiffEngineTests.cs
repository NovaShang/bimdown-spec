using Autodesk.Revit.DB;
using BimDown.RevitAddin.Import;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class DiffEngineTests : RevitApiTest
{
    [Test]
    public async Task Diff_AllNew_WhenNoModelElements()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var csvRows = new List<Dictionary<string, string?>>
            {
                new() { ["id"] = "aaa" },
                new() { ["id"] = "bbb" },
            };

            var result = DiffEngine.Diff(csvRows, []);
            await Assert.That(result.ToCreate.Count).IsEqualTo(2);
            await Assert.That(result.ToUpdate.Count).IsEqualTo(0);
            await Assert.That(result.ToDelete.Count).IsEqualTo(0);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Diff_AllDelete_WhenNoCsvRows()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var levels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            if (levels.Count == 0) return;

            var result = DiffEngine.Diff([], levels);
            await Assert.That(result.ToCreate.Count).IsEqualTo(0);
            await Assert.That(result.ToUpdate.Count).IsEqualTo(0);
            await Assert.That(result.ToDelete.Count).IsEqualTo(levels.Count);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Diff_MatchById_ProducesUpdate()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var levels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            if (levels.Count == 0) return;

            var matchedLevel = levels[0];
            var csvRows = new List<Dictionary<string, string?>>
            {
                new() { ["id"] = matchedLevel.UniqueId, ["name"] = "Updated" },
            };

            var result = DiffEngine.Diff(csvRows, levels);
            await Assert.That(result.ToUpdate.Count).IsEqualTo(1);
            await Assert.That(result.ToUpdate[0].Element.UniqueId).IsEqualTo(matchedLevel.UniqueId);
            await Assert.That(result.ToCreate.Count).IsEqualTo(0);
            await Assert.That(result.ToDelete.Count).IsEqualTo(levels.Count - 1);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Diff_MixedOperations()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var levels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            if (levels.Count == 0) return;

            var csvRows = new List<Dictionary<string, string?>>
            {
                new() { ["id"] = levels[0].UniqueId, ["name"] = "Existing" },
                new() { ["id"] = "brand-new-id", ["name"] = "New" },
            };

            var result = DiffEngine.Diff(csvRows, levels);
            await Assert.That(result.ToUpdate.Count).IsEqualTo(1);
            await Assert.That(result.ToCreate.Count).IsEqualTo(1);
            await Assert.That(result.ToDelete.Count).IsEqualTo(levels.Count - 1);
        }
        finally
        {
            doc.Close(false);
        }
    }

    [Test]
    public async Task Diff_NullId_TreatedAsNew()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var csvRows = new List<Dictionary<string, string?>>
            {
                new() { ["id"] = null, ["name"] = "No ID" },
            };

            var result = DiffEngine.Diff(csvRows, []);
            await Assert.That(result.ToCreate.Count).IsEqualTo(1);
        }
        finally
        {
            doc.Close(false);
        }
    }
}
