namespace BimDown.RevitAddin;

static class UnitConverter
{
    const double FeetToMeters = 0.3048;
    const double RadiansToDegrees = 180.0 / Math.PI;
    const double CubicFeetToCubicMeters = FeetToMeters * FeetToMeters * FeetToMeters;
    const double SquareFeetToSquareMeters = FeetToMeters * FeetToMeters;

    public static double Length(double feet) => feet * FeetToMeters;
    public static double Area(double sqFeet) => sqFeet * SquareFeetToSquareMeters;
    public static double Volume(double cubicFeet) => cubicFeet * CubicFeetToCubicMeters;
    public static double Angle(double radians) => radians * RadiansToDegrees;

    // Reverse conversions (metric → Revit internal units)
    public static double LengthToFeet(double meters) => meters / FeetToMeters;
    public static double AreaToSqFeet(double sqMeters) => sqMeters / SquareFeetToSquareMeters;
    public static double AngleToRadians(double degrees) => degrees / RadiansToDegrees;

    public static string FormatDouble(double value) => Math.Round(value, 3).ToString("G");
    public static string? FormatNullable(double? value) => value is { } v ? FormatDouble(v) : null;

    public static double ParseDouble(string value) => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    public static double? ParseNullableDouble(string? value) =>
        string.IsNullOrEmpty(value) ? null : ParseDouble(value);
}
