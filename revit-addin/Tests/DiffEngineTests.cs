using Autodesk.Revit.DB;
using BimDown.RevitAddin;
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
                new() { ["id"] = "w-1" },
                new() { ["id"] = "w-2" },
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

            // Tag levels with BimDown_Id so they participate in diff
            using (var tx = new Transaction(doc, "Set BimDown_Id"))
            {
                tx.Start();
                BimDownParameter.EnsureParameter(doc);
                for (var i = 0; i < levels.Count; i++)
                    BimDownParameter.Set(levels[i], $"lv-{i + 1}");
                tx.Commit();
            }

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

            // Tag levels with BimDown_Id
            using (var tx = new Transaction(doc, "Set BimDown_Id"))
            {
                tx.Start();
                BimDownParameter.EnsureParameter(doc);
                for (var i = 0; i < levels.Count; i++)
                    BimDownParameter.Set(levels[i], $"lv-{i + 1}");
                tx.Commit();
            }

            var csvRows = new List<Dictionary<string, string?>>
            {
                new() { ["id"] = "lv-1", ["name"] = "Updated" },
            };

            var result = DiffEngine.Diff(csvRows, levels);
            await Assert.That(result.ToUpdate.Count).IsEqualTo(1);
            await Assert.That(BimDownParameter.Get(result.ToUpdate[0].Element)).IsEqualTo("lv-1");
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

            // Tag levels with BimDown_Id
            using (var tx = new Transaction(doc, "Set BimDown_Id"))
            {
                tx.Start();
                BimDownParameter.EnsureParameter(doc);
                for (var i = 0; i < levels.Count; i++)
                    BimDownParameter.Set(levels[i], $"lv-{i + 1}");
                tx.Commit();
            }

            var csvRows = new List<Dictionary<string, string?>>
            {
                new() { ["id"] = "lv-1", ["name"] = "Existing" },
                new() { ["id"] = "lv-999", ["name"] = "New" },
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

    [Test]
    public async Task Diff_UntaggedElements_NotDeleted()
    {
        var doc = RevitTestHelper.CreateTempDocument(Application);
        try
        {
            var levels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            if (levels.Count == 0) return;

            // Don't set BimDown_Id — elements without it should not be deleted
            var result = DiffEngine.Diff([], levels);
            await Assert.That(result.ToDelete.Count).IsEqualTo(0);
        }
        finally
        {
            doc.Close(false);
        }
    }
}
