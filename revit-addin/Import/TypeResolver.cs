using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

static class TypeResolver
{
    public static WallType ResolveOrCreateWallType(Document doc, double thicknessMeters)
    {
        var thicknessMm = (int)Math.Round(thicknessMeters * 1000);
        var targetName = $"BimDown_{thicknessMm}mm";
        var thicknessFeet = UnitConverter.LengthToFeet(thicknessMeters);

        // Try to find existing type with matching name
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Name == targetName);
        if (existing is not null) return existing;

        // Try to find a basic wall type to duplicate
        var template = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

        if (template is null)
            throw new InvalidOperationException("No basic WallType found to duplicate");

        var newType = (WallType)template.Duplicate(targetName);

        // Set width on the compound structure
        var cs = newType.GetCompoundStructure();
        if (cs is not null)
        {
            var layers = cs.GetLayers();
            if (layers.Count > 0)
            {
                cs.SetLayerWidth(0, thicknessFeet);
                newType.SetCompoundStructure(cs);
            }
        }

        return newType;
    }

    public static FloorType ResolveOrCreateFloorType(Document doc, double thicknessMeters)
    {
        var thicknessMm = (int)Math.Round(thicknessMeters * 1000);
        var targetName = $"BimDown_{thicknessMm}mm";
        var thicknessFeet = UnitConverter.LengthToFeet(thicknessMeters);

        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(ft => ft.Name == targetName);
        if (existing is not null) return existing;

        var template = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault();

        if (template is null)
            throw new InvalidOperationException("No FloorType found to duplicate");

        var newType = (FloorType)template.Duplicate(targetName);

        var cs = newType.GetCompoundStructure();
        if (cs is not null)
        {
            var layers = cs.GetLayers();
            if (layers.Count > 0)
            {
                cs.SetLayerWidth(0, thicknessFeet);
                newType.SetCompoundStructure(cs);
            }
        }

        return newType;
    }

    public static FamilySymbol ResolveOrCreateColumnType(Document doc, string? shape, double sizeXMeters, double sizeYMeters)
    {
        var sizeXMm = (int)Math.Round(sizeXMeters * 1000);
        var sizeYMm = (int)Math.Round(sizeYMeters * 1000);
        var shapeStr = shape ?? "rect";
        var targetName = $"BimDown_{shapeStr}_{sizeXMm}x{sizeYMm}";

        var existing = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Columns)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(fs => fs.Name == targetName);
        if (existing is not null)
        {
            if (!existing.IsActive) existing.Activate();
            return existing;
        }

        var template = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Columns)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault();

        if (template is null)
            throw new InvalidOperationException("No column FamilySymbol found to duplicate");

        var newType = (FamilySymbol)template.Duplicate(targetName);
        if (!newType.IsActive) newType.Activate();
        return newType;
    }

    public static FamilySymbol ResolveOrCreateDoorType(Document doc, double widthMeters, double heightMeters)
    {
        var wMm = (int)Math.Round(widthMeters * 1000);
        var hMm = (int)Math.Round(heightMeters * 1000);
        var targetName = $"BimDown_{wMm}x{hMm}";

        return ResolveOrCreateOpeningType(doc, BuiltInCategory.OST_Doors, targetName, widthMeters, heightMeters);
    }

    public static FamilySymbol ResolveOrCreateWindowType(Document doc, double widthMeters, double heightMeters)
    {
        var wMm = (int)Math.Round(widthMeters * 1000);
        var hMm = (int)Math.Round(heightMeters * 1000);
        var targetName = $"BimDown_{wMm}x{hMm}";

        return ResolveOrCreateOpeningType(doc, BuiltInCategory.OST_Windows, targetName, widthMeters, heightMeters);
    }

    public static FamilySymbol ResolveOrCreateStructuralColumnType(Document doc, string? shape, double sizeXMeters, double sizeYMeters)
    {
        var sizeXMm = (int)Math.Round(sizeXMeters * 1000);
        var sizeYMm = (int)Math.Round(sizeYMeters * 1000);
        var shapeStr = shape ?? "rect";
        var targetName = $"BimDown_struct_{shapeStr}_{sizeXMm}x{sizeYMm}";

        var existing = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(fs => fs.Name == targetName);
        if (existing is not null)
        {
            if (!existing.IsActive) existing.Activate();
            return existing;
        }

        var template = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No structural column FamilySymbol found to duplicate");

        var newType = (FamilySymbol)template.Duplicate(targetName);
        if (!newType.IsActive) newType.Activate();
        return newType;
    }

    public static FamilySymbol ResolveOrCreateStructuralFramingType(Document doc, string? shape, double sizeXMeters, double sizeYMeters)
    {
        var sizeXMm = (int)Math.Round(sizeXMeters * 1000);
        var sizeYMm = (int)Math.Round(sizeYMeters * 1000);
        var shapeStr = shape ?? "rect";
        var targetName = $"BimDown_framing_{shapeStr}_{sizeXMm}x{sizeYMm}";

        var existing = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(fs => fs.Name == targetName);
        if (existing is not null)
        {
            if (!existing.IsActive) existing.Activate();
            return existing;
        }

        var template = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No structural framing FamilySymbol found to duplicate");

        var newType = (FamilySymbol)template.Duplicate(targetName);
        if (!newType.IsActive) newType.Activate();
        return newType;
    }

    public static WallType FindCurtainWallType(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Kind == WallKind.Curtain)
            ?? throw new InvalidOperationException("No curtain WallType found");
    }

    public static FamilySymbol FindFirstFamilySymbol(Document doc, BuiltInCategory category)
    {
        var symbol = new FilteredElementCollector(doc)
            .OfCategory(category)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No FamilySymbol found for {category}");
        if (!symbol.IsActive) symbol.Activate();
        return symbol;
    }

    static FamilySymbol ResolveOrCreateOpeningType(Document doc, BuiltInCategory category, string targetName,
        double widthMeters, double heightMeters)
    {
        var existing = new FilteredElementCollector(doc)
            .OfCategory(category)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(fs => fs.Name == targetName);
        if (existing is not null)
        {
            if (!existing.IsActive) existing.Activate();
            return existing;
        }

        var template = new FilteredElementCollector(doc)
            .OfCategory(category)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault();

        if (template is null)
            throw new InvalidOperationException($"No FamilySymbol found for {category} to duplicate");

        var newType = (FamilySymbol)template.Duplicate(targetName);

        // Try to set width/height parameters
        var widthFeet = UnitConverter.LengthToFeet(widthMeters);
        var heightFeet = UnitConverter.LengthToFeet(heightMeters);

        newType.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.Set(widthFeet);
        newType.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.Set(heightFeet);
        newType.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.Set(widthFeet);
        newType.get_Parameter(BuiltInParameter.DOOR_HEIGHT)?.Set(heightFeet);
        newType.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.Set(widthFeet);
        newType.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)?.Set(heightFeet);

        if (!newType.IsActive) newType.Activate();
        return newType;
    }
}
