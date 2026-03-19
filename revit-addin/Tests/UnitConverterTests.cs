using BimDown.RevitAddin;
using Nice3point.TUnit.Revit;

namespace BimDown.RevitTests;

public class UnitConverterTests : RevitApiTest
{
    [Test]
    public async Task Length_FeetToMeters()
    {
        var meters = UnitConverter.Length(1.0);
        await Assert.That(meters).IsEqualTo(0.3048).Within(1e-10);
    }

    [Test]
    public async Task Length_RoundTrip()
    {
        const double originalFeet = 32.8084;
        var meters = UnitConverter.Length(originalFeet);
        var backToFeet = UnitConverter.LengthToFeet(meters);
        await Assert.That(backToFeet).IsEqualTo(originalFeet).Within(1e-8);
    }

    [Test]
    public async Task Area_SquareFeetToSquareMeters()
    {
        var sqm = UnitConverter.Area(1.0);
        await Assert.That(sqm).IsEqualTo(0.3048 * 0.3048).Within(1e-10);
    }

    [Test]
    public async Task Volume_CubicFeetToCubicMeters()
    {
        var cbm = UnitConverter.Volume(1.0);
        await Assert.That(cbm).IsEqualTo(0.3048 * 0.3048 * 0.3048).Within(1e-10);
    }

    [Test]
    public async Task Angle_RadiansToDegrees()
    {
        var degrees = UnitConverter.Angle(Math.PI);
        await Assert.That(degrees).IsEqualTo(180.0).Within(1e-10);
    }

    [Test]
    public async Task Angle_RoundTrip()
    {
        const double originalRadians = 1.5707963;
        var degrees = UnitConverter.Angle(originalRadians);
        var backToRadians = UnitConverter.AngleToRadians(degrees);
        await Assert.That(backToRadians).IsEqualTo(originalRadians).Within(1e-6);
    }

    [Test]
    public async Task FormatDouble_UsesG10()
    {
        var result = UnitConverter.FormatDouble(1.23456789012345);
        await Assert.That(result).IsEqualTo("1.23456789");
    }

    [Test]
    public async Task ParseDouble_InvariantCulture()
    {
        var result = UnitConverter.ParseDouble("3.14159");
        await Assert.That(result).IsEqualTo(3.14159).Within(1e-10);
    }

    [Test]
    public async Task ParseNullableDouble_Null_ReturnsNull()
    {
        var result = UnitConverter.ParseNullableDouble(null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseNullableDouble_Empty_ReturnsNull()
    {
        var result = UnitConverter.ParseNullableDouble("");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseNullableDouble_Valid_ReturnsValue()
    {
        var result = UnitConverter.ParseNullableDouble("2.718");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(2.718).Within(1e-10);
    }
}
