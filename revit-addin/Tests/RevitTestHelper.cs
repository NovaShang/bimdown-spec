using Autodesk.Revit.DB;
using Application = Autodesk.Revit.ApplicationServices.Application;
using BimDown.RevitAddin;
using BimDown.RevitAddin.Import;

namespace BimDown.RevitTests;

static class RevitTestHelper
{
    public static (IReadOnlyList<string> Columns, List<Dictionary<string, string?>> Rows)
        RoundTripCsv(IReadOnlyList<string> columns, List<Dictionary<string, string?>> rows)
    {
        var path = Path.GetTempFileName();
        try
        {
            CsvWriter.Write(path, columns, rows);
            return CsvReader.Read(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static IdMap BuildIdMap(Document doc)
    {
        var needsTx = !doc.IsModifiable;
        Transaction? tx = null;
        if (needsTx)
        {
            tx = new Transaction(doc, "Setup BimDown IDs");
            tx.Start();
        }

        BimDownParameter.EnsureParameter(doc);

        var idMap = new IdMap();
        var levelCounter = 0;
        foreach (var el in new FilteredElementCollector(doc)
                     .OfCategory(BuiltInCategory.OST_Levels)
                     .WhereElementIsNotElementType()
                     .OrderBy(e => e.Id.Value))
        {
            var shortId = $"lv-{++levelCounter}";
            BimDownParameter.Set(el, shortId);
            idMap.Register(shortId, el.Id);
        }

        var gridCounter = 0;
        foreach (var el in new FilteredElementCollector(doc)
                     .OfCategory(BuiltInCategory.OST_Grids)
                     .WhereElementIsNotElementType()
                     .OrderBy(e => e.Id.Value))
        {
            var shortId = $"gr-{++gridCounter}";
            BimDownParameter.Set(el, shortId);
            idMap.Register(shortId, el.Id);
        }

        if (needsTx)
        {
            tx!.Commit();
            tx.Dispose();
        }

        return idMap;
    }

    /// <summary>
    /// Tags an element with a BimDown_Id. If no transaction is active, creates one.
    /// </summary>
    public static void TagElement(Document doc, Element element, string shortId)
    {
        var needsTx = !doc.IsModifiable;
        Transaction? tx = null;
        if (needsTx)
        {
            tx = new Transaction(doc, "Tag BimDown ID");
            tx.Start();
        }

        BimDownParameter.EnsureParameter(doc);
        BimDownParameter.Set(element, shortId);

        if (needsTx)
        {
            tx!.Commit();
            tx.Dispose();
        }
    }

    public static void AssertClose(double expected, double actual, double tolerance = 1e-6, string? message = null)
    {
        var diff = Math.Abs(expected - actual);
        if (diff > tolerance)
            throw new Exception(
                $"Expected {expected} but got {actual} (diff={diff}){(message is not null ? $": {message}" : "")}");
    }

    public static Document CreateTempDocument(Application app) => app.NewProjectDocument(UnitSystem.Metric);

    /// <summary>
    /// Ensures a loadable family is available for the given category.
    /// NewProjectDocument(UnitSystem.Metric) creates a minimal doc without loadable families,
    /// so we create one from the Revit family template and load it.
    /// </summary>
    public static void EnsureFamilyLoaded(Document doc, Application app, BuiltInCategory category)
    {
        if (new FilteredElementCollector(doc)
                .OfCategory(category)
                .OfClass(typeof(FamilySymbol))
                .Any())
            return;

        var templateDir = app.FamilyTemplatePath;
        if (string.IsNullOrEmpty(templateDir) || !Directory.Exists(templateDir))
            throw new InvalidOperationException(
                $"Family template path not available — cannot create family for {category}");

        // Search for a matching family template (.rft)
        var searchTerms = category switch
        {
            BuiltInCategory.OST_Columns => ["Column"],
            BuiltInCategory.OST_StructuralColumns => ["Structural Column", "Column"],
            BuiltInCategory.OST_StructuralFraming => ["Framing", "Beam"],
            BuiltInCategory.OST_StructuralFoundation => ["Foundation"],
            _ => Array.Empty<string>()
        };

        string? rftPath = null;
        foreach (var term in searchTerms)
        {
            rftPath = Directory.EnumerateFiles(templateDir, "*.rft", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                    .Contains(term, StringComparison.OrdinalIgnoreCase));
            if (rftPath is not null) break;
        }

        // Fallback: try any template
        rftPath ??= Directory.EnumerateFiles(templateDir, "*.rft", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (rftPath is null)
            throw new InvalidOperationException(
                $"No family template (.rft) found in {templateDir} for {category}");

        var familyDoc = app.NewFamilyDocument(rftPath);
        var tempRfa = Path.Combine(Path.GetTempPath(), $"BimDown_Test_{Guid.NewGuid():N}.rfa");
        try
        {
            familyDoc.SaveAs(tempRfa);
            familyDoc.Close(false);
            doc.LoadFamily(tempRfa, out _);
        }
        finally
        {
            if (File.Exists(tempRfa)) File.Delete(tempRfa);
        }

        if (!new FilteredElementCollector(doc)
                .OfCategory(category)
                .OfClass(typeof(FamilySymbol))
                .Any())
            throw new InvalidOperationException(
                $"Loaded family from {rftPath} but no FamilySymbol found for {category}");
    }

    /// <summary>
    /// Collects existing elements as CSV rows so DiffEngine won't try to delete them.
    /// Uses BimDown_Id for element IDs.
    /// </summary>
    public static List<Dictionary<string, string?>> PreserveExistingElements(
        Document doc, BuiltInCategory category, Func<Element, Dictionary<string, string?>> toRow)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .Select(toRow)
            .ToList();
    }
}
