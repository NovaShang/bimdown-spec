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

    public static string FormatDouble(double value) => value.ToString("G10");
    public static string? FormatNullable(double? value) => value is { } v ? FormatDouble(v) : null;
}
