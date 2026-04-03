using System.IO;
using Autodesk.Revit.DB;

namespace BimDown.RevitAddin;

static class BimDownParameter
{
    const string ParamName = "BimDown_Id";
    static readonly Guid ParamGuid = new("7c3f8a2b-1d4e-5f60-9a7b-2c3d4e5f6a7b");

    internal static readonly BuiltInCategory[] AllCategories =
    [
        BuiltInCategory.OST_Levels,
        BuiltInCategory.OST_Grids,
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Columns,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_Rooms,
        BuiltInCategory.OST_Doors,
        BuiltInCategory.OST_Windows,
        BuiltInCategory.OST_Stairs,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_StructuralFoundation,
        BuiltInCategory.OST_DuctCurves,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_CableTray,
        BuiltInCategory.OST_Conduit,
        BuiltInCategory.OST_MechanicalEquipment,
        BuiltInCategory.OST_ElectricalEquipment,
        BuiltInCategory.OST_DuctTerminal,
        BuiltInCategory.OST_Sprinklers,
        BuiltInCategory.OST_LightingFixtures,
        BuiltInCategory.OST_ElectricalFixtures,
        // V2 new categories
        BuiltInCategory.OST_Ramps,
        BuiltInCategory.OST_Ceilings,
        BuiltInCategory.OST_SWallRectOpening,
        BuiltInCategory.OST_FloorOpening,
        BuiltInCategory.OST_ShaftOpening,
        BuiltInCategory.OST_StairsRailing,
        BuiltInCategory.OST_GenericModel,
        BuiltInCategory.OST_Planting,
        BuiltInCategory.OST_Site,
        // MEP fitting/accessory categories (mep_node)
        BuiltInCategory.OST_DuctFitting,
        BuiltInCategory.OST_PipeFitting,
        BuiltInCategory.OST_CableTrayFitting,
        BuiltInCategory.OST_ConduitFitting,
        BuiltInCategory.OST_DuctAccessory,
        BuiltInCategory.OST_PipeAccessory,
    ];

    internal static void EnsureParameter(Document doc)
    {
        if (SharedParameterElement.Lookup(doc, ParamGuid) is not null)
            return;

        var app = doc.Application;
        var originalFile = app.SharedParametersFilename;
        var tempFile = Path.GetTempFileName();

        try
        {
            app.SharedParametersFilename = tempFile;
            var defFile = app.OpenSharedParameterFile();
            var group = defFile.Groups.Create("BimDown");
            var def = group.Definitions.Create(
                new ExternalDefinitionCreationOptions(ParamName, SpecTypeId.String.Text) { GUID = ParamGuid });

            var catSet = new CategorySet();
            foreach (var bic in AllCategories)
            {
                var cat = doc.Settings.Categories.get_Item(bic);
                if (cat is not null) catSet.Insert(cat);
            }

            doc.ParameterBindings.Insert(def, new InstanceBinding(catSet));
        }
        finally
        {
            app.SharedParametersFilename = originalFile;
            try { File.Delete(tempFile); } catch { }
        }
    }

    internal static string? Get(Element element)
    {
        var param = element.get_Parameter(ParamGuid);
        var val = param?.AsString();
        return string.IsNullOrEmpty(val) ? null : val;
    }

    internal static void Set(Element element, string shortId)
    {
        var param = element.get_Parameter(ParamGuid);
        param?.Set(shortId);
    }
}
