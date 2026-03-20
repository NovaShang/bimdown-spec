using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;

namespace BimDown.RevitAddin.Import;

class WallImporter() : TableImporterBase(
    "wall",
    10,
    [BuiltInCategory.OST_Walls],
    e => e is Wall w && w.WallType.Kind == WallKind.Basic &&
         w.StructuralUsage == StructuralWallUsage.NonBearing)
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        var line = ParseWallLine(row);
        var levelId = IdMap.Resolve(doc, row.GetValueOrDefault("level_id"))
            ?? throw new InvalidOperationException("level_id is required");

        var thicknessStr = row.GetValueOrDefault("thickness");
        WallType? wallType = null;
        if (thicknessStr is not null)
        {
            var thickness = UnitConverter.ParseDouble(thicknessStr);
            wallType = TypeResolver.ResolveOrCreateWallType(doc, thickness);
        }

        var heightStr = row.GetValueOrDefault("height");
        var heightFeet = heightStr is not null
            ? UnitConverter.LengthToFeet(UnitConverter.ParseDouble(heightStr))
            : 10.0; // default 10 feet

        var typeId = wallType?.Id ?? GetDefaultWallTypeId(doc);
        var wall = Wall.Create(doc, line, typeId, levelId, heightFeet, 0, false, false);

        var baseOffsetStr = row.GetValueOrDefault("base_offset");
        if (baseOffsetStr is not null)
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(baseOffsetStr)));

        var topLevelId = IdMap.Resolve(doc, row.GetValueOrDefault("top_level_id"));
        if (topLevelId is not null)
            wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(topLevelId);

        var topOffsetStr = row.GetValueOrDefault("top_offset");
        if (topOffsetStr is not null)
            wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(topOffsetStr)));

        SetMark(wall, row);
        return wall;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        if (element is not Wall wall) return;

        // Update location curve endpoints
        if (wall.Location is LocationCurve lc)
        {
            var newLine = ParseWallLine(row);
            lc.Curve = newLine;
        }

        // Update height
        var heightStr = row.GetValueOrDefault("height");
        if (heightStr is not null)
            wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(heightStr)));

        // Update base offset
        var baseOffsetStr = row.GetValueOrDefault("base_offset");
        if (baseOffsetStr is not null)
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(baseOffsetStr)));

        // Update top level
        var topLevelId = IdMap.Resolve(doc, row.GetValueOrDefault("top_level_id"));
        if (topLevelId is not null)
            wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(topLevelId);

        // Update top offset
        var topOffsetStr = row.GetValueOrDefault("top_offset");
        if (topOffsetStr is not null)
            wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(topOffsetStr)));

        // Update type if thickness changed
        var thicknessStr = row.GetValueOrDefault("thickness");
        if (thicknessStr is not null)
        {
            var thickness = UnitConverter.ParseDouble(thicknessStr);
            var newType = TypeResolver.ResolveOrCreateWallType(doc, thickness);
            if (wall.WallType.Id != newType.Id)
                wall.WallType = newType;
        }

        SetMark(wall, row);
    }

    static Line ParseWallLine(Dictionary<string, string?> row)
    {
        var sx = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["start_x"]!));
        var sy = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["start_y"]!));
        var ex = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["end_x"]!));
        var ey = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["end_y"]!));
        return Line.CreateBound(new XYZ(sx, sy, 0), new XYZ(ex, ey, 0));
    }

    static ElementId GetDefaultWallTypeId(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .First(wt => wt.Kind == WallKind.Basic).Id;
    }
}

class ColumnImporter() : TableImporterBase("column", 10, [BuiltInCategory.OST_Columns])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        var x = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["x"]!));
        var y = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["y"]!));
        var levelId = IdMap.Resolve(doc, row.GetValueOrDefault("level_id"))
            ?? throw new InvalidOperationException("level_id is required");
        var level = (Level)doc.GetElement(levelId);

        var shape = row.GetValueOrDefault("shape");
        var sizeXStr = row.GetValueOrDefault("size_x");
        var sizeYStr = row.GetValueOrDefault("size_y");

        FamilySymbol symbol;
        if (sizeXStr is not null && sizeYStr is not null)
        {
            symbol = TypeResolver.ResolveOrCreateColumnType(doc, shape,
                UnitConverter.ParseDouble(sizeXStr), UnitConverter.ParseDouble(sizeYStr));
        }
        else
        {
            symbol = GetDefaultColumnSymbol(doc);
        }

        var pt = new XYZ(x, y, level.Elevation);
        var column = doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural);

        // Set rotation
        var rotationStr = row.GetValueOrDefault("rotation");
        if (rotationStr is not null)
        {
            var angle = UnitConverter.AngleToRadians(UnitConverter.ParseDouble(rotationStr));
            if (Math.Abs(angle) > 1e-10)
            {
                var axis = Line.CreateBound(pt, pt + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, column.Id, axis, angle);
            }
        }

        // Set vertical span
        var topLevelId = IdMap.Resolve(doc, row.GetValueOrDefault("top_level_id"));
        if (topLevelId is not null)
            column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLevelId);

        var topOffsetStr = row.GetValueOrDefault("top_offset");
        if (topOffsetStr is not null)
            column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(topOffsetStr)));

        var baseOffsetStr = row.GetValueOrDefault("base_offset");
        if (baseOffsetStr is not null)
            column.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(baseOffsetStr)));

        SetMark(column, row);
        return column;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        if (element is not FamilyInstance fi) return;

        // Update location using MoveElement (lp.Point setter can misfire on level-hosted columns)
        if (fi.Location is LocationPoint lp)
        {
            var x = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["x"]!));
            var y = UnitConverter.LengthToFeet(UnitConverter.ParseDouble(row["y"]!));
            var target = new XYZ(x, y, lp.Point.Z);
            var delta = target - lp.Point;
            if (delta.GetLength() > 1e-10)
                ElementTransformUtils.MoveElement(doc, fi.Id, delta);
        }

        // Update rotation
        var rotationStr = row.GetValueOrDefault("rotation");
        if (rotationStr is not null && fi.Location is LocationPoint locPt)
        {
            var targetAngle = UnitConverter.AngleToRadians(UnitConverter.ParseDouble(rotationStr));
            var currentAngle = locPt.Rotation;
            var angleDelta = targetAngle - currentAngle;
            if (Math.Abs(angleDelta) > 1e-10)
            {
                var axis = Line.CreateBound(locPt.Point, locPt.Point + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, fi.Id, axis, angleDelta);
            }
        }

        // Update vertical span
        var topLevelId = IdMap.Resolve(doc, row.GetValueOrDefault("top_level_id"));
        if (topLevelId is not null)
            fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLevelId);

        var topOffsetStr = row.GetValueOrDefault("top_offset");
        if (topOffsetStr is not null)
            fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(topOffsetStr)));

        var baseOffsetStr = row.GetValueOrDefault("base_offset");
        if (baseOffsetStr is not null)
            fi.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(baseOffsetStr)));

        SetMark(fi, row);
    }

    static FamilySymbol GetDefaultColumnSymbol(Document doc)
    {
        var symbol = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Columns)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .First();
        if (!symbol.IsActive) symbol.Activate();
        return symbol;
    }
}

class SlabImporter() : TableImporterBase(
    "slab",
    15,
    [BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs],
    e =>
    {
        if (e is Floor floor)
        {
            var structural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger();
            return structural != 1;
        }
        return true;
    })
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        var function = row.GetValueOrDefault("function") ?? "floor";
        if (function == "roof") return null; // Skip roof creation in v1

        var pointsJson = row.GetValueOrDefault("points")
            ?? throw new InvalidOperationException("points is required for slab");
        var points = GeometryUtils.DeserializePolygon(pointsJson);
        if (points.Count < 3) throw new InvalidOperationException("Need at least 3 points for slab");

        var levelId = IdMap.Resolve(doc, row.GetValueOrDefault("level_id"))
            ?? throw new InvalidOperationException("level_id is required");
        var level = (Level)doc.GetElement(levelId);

        // Resolve floor type
        var thicknessStr = row.GetValueOrDefault("thickness");
        FloorType floorType;
        if (thicknessStr is not null)
        {
            var thickness = UnitConverter.ParseDouble(thicknessStr);
            floorType = TypeResolver.ResolveOrCreateFloorType(doc, thickness);
        }
        else
        {
            floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .First();
        }

        // Build curve loop
        var curveLoop = new CurveLoop();
        for (var i = 0; i < points.Count; i++)
        {
            var p1 = new XYZ(points[i].X, points[i].Y, level.Elevation);
            var p2 = new XYZ(points[(i + 1) % points.Count].X, points[(i + 1) % points.Count].Y, level.Elevation);
            curveLoop.Append(Line.CreateBound(p1, p2));
        }

        var floor = Floor.Create(doc, [curveLoop], floorType.Id, levelId);

        var baseOffsetStr = row.GetValueOrDefault("base_offset");
        if (baseOffsetStr is not null)
            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(baseOffsetStr)));

        SetMark(floor, row);
        return floor;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        if (element is not Floor floor) return;

        // Slab geometry can't be edited in-place — delete and recreate if points changed
        var pointsJson = row.GetValueOrDefault("points");
        if (pointsJson is not null)
        {
            doc.Delete(floor.Id);
            var newFloor = CreateElement(doc, row);
            if (newFloor is not null)
            {
                var csvId = row.GetValueOrDefault("id");
                if (csvId is not null)
                {
                    BimDownParameter.Set(newFloor, csvId);
                    IdMap.Register(csvId, newFloor.Id);
                }
            }
            return;
        }

        // If no geometry change, update type and offset only
        var thicknessStr = row.GetValueOrDefault("thickness");
        if (thicknessStr is not null)
        {
            var thickness = UnitConverter.ParseDouble(thicknessStr);
            var newType = TypeResolver.ResolveOrCreateFloorType(doc, thickness);
            if (floor.FloorType.Id != newType.Id)
                floor.FloorType = newType;
        }

        var baseOffsetStr = row.GetValueOrDefault("base_offset");
        if (baseOffsetStr is not null)
            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(baseOffsetStr)));

        SetMark(floor, row);
    }
}

class SpaceImporter() : TableImporterBase("space", 15, [BuiltInCategory.OST_Rooms])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        var levelId = IdMap.Resolve(doc, row.GetValueOrDefault("level_id"))
            ?? throw new InvalidOperationException("level_id is required");
        var level = (Level)doc.GetElement(levelId);

        // Parse centroid from polygon points for UV placement
        var pointsJson = row.GetValueOrDefault("points");
        UV uv;
        if (pointsJson is not null)
        {
            var points = GeometryUtils.DeserializePolygon(pointsJson);
            var cx = points.Average(p => p.X);
            var cy = points.Average(p => p.Y);
            uv = new UV(cx, cy);
        }
        else
        {
            uv = new UV(0, 0);
        }

        var room = doc.Create.NewRoom(level, uv);

        var name = row.GetValueOrDefault("name");
        if (name is not null) room.Name = name;

        SetMark(room, row);
        return room;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        if (element is not Room room) return;

        var name = row.GetValueOrDefault("name");
        if (name is not null && room.Name != name) room.Name = name;

        SetMark(room, row);
    }
}

class DoorImporter() : TableImporterBase("door", 20, [BuiltInCategory.OST_Doors])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        return HostedOpeningHelper.CreateHostedOpening(doc, row, IdMap, BuiltInCategory.OST_Doors, isWindow: false);
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        HostedOpeningHelper.UpdateHostedOpening(doc, row, element, IdMap, isWindow: false);
    }
}

class WindowImporter() : TableImporterBase("window", 20, [BuiltInCategory.OST_Windows])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        return HostedOpeningHelper.CreateHostedOpening(doc, row, IdMap, BuiltInCategory.OST_Windows, isWindow: true);
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        HostedOpeningHelper.UpdateHostedOpening(doc, row, element, IdMap, isWindow: true);
    }
}

class StairImporter() : TableImporterBase("stair", 15, [BuiltInCategory.OST_Stairs])
{
    protected override Element? CreateElement(Document doc, Dictionary<string, string?> row)
    {
        // StairsEditScope is too complex for CSV-driven creation — skip
        return null;
    }

    protected override void UpdateElement(Document doc, Dictionary<string, string?> row, Element element)
    {
        var widthStr = row.GetValueOrDefault("width");
        if (widthStr is not null)
            element.get_Parameter(BuiltInParameter.STAIRS_ATTR_TREAD_WIDTH)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(widthStr)));

        // step_count is computed/read-only — skip

        var topLevelId = IdMap.Resolve(doc, row.GetValueOrDefault("top_level_id"));
        if (topLevelId is not null)
            element.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLevelId);

        var topOffsetStr = row.GetValueOrDefault("top_offset");
        if (topOffsetStr is not null)
            element.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(
                UnitConverter.LengthToFeet(UnitConverter.ParseDouble(topOffsetStr)));

        SetMark(element, row);
    }
}

static class HostedOpeningHelper
{
    internal static Element? CreateHostedOpening(Document doc, Dictionary<string, string?> row,
        IdMap idMap, BuiltInCategory category, bool isWindow)
    {
        var hostId = idMap.Resolve(doc, row.GetValueOrDefault("host_id"))
            ?? throw new InvalidOperationException("host_id is required");
        var host = doc.GetElement(hostId) as Wall
            ?? throw new InvalidOperationException("host must be a Wall");

        var levelId = idMap.Resolve(doc, row.GetValueOrDefault("level_id"))
            ?? throw new InvalidOperationException("level_id is required");
        var level = (Level)doc.GetElement(levelId);

        var widthStr = row.GetValueOrDefault("width");
        var heightStr = row.GetValueOrDefault("height");

        FamilySymbol symbol;
        if (widthStr is not null && heightStr is not null)
        {
            var w = UnitConverter.ParseDouble(widthStr);
            var h = UnitConverter.ParseDouble(heightStr);
            symbol = isWindow
                ? TypeResolver.ResolveOrCreateWindowType(doc, w, h)
                : TypeResolver.ResolveOrCreateDoorType(doc, w, h);
        }
        else
        {
            symbol = new FilteredElementCollector(doc)
                .OfCategory(category)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .First();
            if (!symbol.IsActive) symbol.Activate();
        }

        // Calculate insertion point from location_param on host curve
        XYZ insertPt;
        var locationParamStr = row.GetValueOrDefault("location_param");
        if (locationParamStr is not null && host.Location is LocationCurve hostCurve)
        {
            var param = UnitConverter.ParseDouble(locationParamStr);
            var rawParam = hostCurve.Curve.ComputeRawParameter(param);
            insertPt = hostCurve.Curve.Evaluate(rawParam, false);
        }
        else
        {
            // Midpoint of host wall
            if (host.Location is LocationCurve lc)
                insertPt = lc.Curve.Evaluate(0.5, true);
            else
                throw new InvalidOperationException("Cannot determine insertion point");
        }

        var instance = doc.Create.NewFamilyInstance(insertPt, symbol, host, level, StructuralType.NonStructural);
        TableImporterBase.SetMark(instance, row);
        return instance;
    }

    internal static void UpdateHostedOpening(Document doc, Dictionary<string, string?> row,
        Element element, IdMap idMap, bool isWindow)
    {
        if (element is not FamilyInstance fi) return;

        // Update type if width/height changed
        var widthStr = row.GetValueOrDefault("width");
        var heightStr = row.GetValueOrDefault("height");
        if (widthStr is not null && heightStr is not null)
        {
            var w = UnitConverter.ParseDouble(widthStr);
            var h = UnitConverter.ParseDouble(heightStr);
            var newSymbol = isWindow
                ? TypeResolver.ResolveOrCreateWindowType(doc, w, h)
                : TypeResolver.ResolveOrCreateDoorType(doc, w, h);
            if (fi.Symbol.Id != newSymbol.Id)
                fi.Symbol = newSymbol;
        }

        // Update location parameter on host
        var locationParamStr = row.GetValueOrDefault("location_param");
        if (locationParamStr is not null && fi.Host is Wall host && host.Location is LocationCurve hostCurve
            && fi.Location is LocationPoint lp)
        {
            var param = UnitConverter.ParseDouble(locationParamStr);
            var rawParam = hostCurve.Curve.ComputeRawParameter(param);
            var newPt = hostCurve.Curve.Evaluate(rawParam, false);
            lp.Point = newPt;
        }

        TableImporterBase.SetMark(fi, row);
    }
}
