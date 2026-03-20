using System;
using System.Linq;
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

    private static Parameter? FindParameterByNames(Element element, params string[] names)
    {
        foreach (Parameter param in element.Parameters)
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
