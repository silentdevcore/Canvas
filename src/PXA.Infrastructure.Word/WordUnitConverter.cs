namespace PXA.Infrastructure.Word;

internal static class WordUnitConverter
{
    // PXA coordinates are aligned with the PDF model (72 units per inch).
    private const double PxaUnitsPerInch = 72.0;
    private const double TwipsPerInch = 1440.0;
    private const double EmuPerInch = 914400.0;

    internal static int PxaToTwips(double units)
        => (int)Math.Round(units * TwipsPerInch / PxaUnitsPerInch, MidpointRounding.AwayFromZero);

    internal static long PxaToEmu(double units)
        => (long)Math.Round(units * EmuPerInch / PxaUnitsPerInch, MidpointRounding.AwayFromZero);

    internal static int PointsToHalfPoints(double points)
        => (int)Math.Round(points * 2.0, MidpointRounding.AwayFromZero);
}
