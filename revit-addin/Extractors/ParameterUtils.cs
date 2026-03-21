using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public static class ParameterUtils
{
    public static double? FindDoubleParameterByNames(Element element, params string[] names)
    {
        var param = FindParameterByNames(element, names);
        return param?.AsDouble();
    }

    public static string? FindStringParameterByNames(Element element, params string[] names)
    {
        var param = FindParameterByNames(element, names);
        return param?.AsString();
    }

    /// <summary>
    /// Returns the double value only if it is positive (> 0).
    /// Useful for dimension parameters where 0 means "not set".
    /// </summary>
    public static double? AsPositiveDouble(this Parameter? param)
    {
        var val = param?.AsDouble();
        return val is > 0 ? val : null;
    }

    private static Parameter? FindParameterByNames(Element element, params string[] names)
    {
        var result = SearchParameters(element.Parameters, names);
        if (result is not null) return result;

        // Also search type parameters (FamilySymbol / ElementType)
        var typeId = element.GetTypeId();
        if (typeId is not null && typeId != ElementId.InvalidElementId)
        {
            var typeElement = element.Document.GetElement(typeId);
            if (typeElement is not null)
                return SearchParameters(typeElement.Parameters, names);
        }
        return null;
    }

    private static Parameter? SearchParameters(ParameterSet parameters, string[] names)
    {
        foreach (Parameter param in parameters)
        {
            var pName = param.Definition.Name;
            if (names.Any(n => string.Equals(n, pName, StringComparison.OrdinalIgnoreCase)))
            {
                if (param.HasValue) return param;
            }
        }
        return null;
    }
}
