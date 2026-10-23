namespace PVAD.Infrastructure.Word;

internal static class WordUnitConverter
{
    // PVAD coordinates are aligned with the PDF model (72 units per inch).
    private const double PvadUnitsPerInch = 72.0;
    private const double TwipsPerInch = 1440.0;
    private const double EmuPerInch = 914400.0;

    internal static int PvadToTwips(double units)
        => (int)Math.Round(units * TwipsPerInch / PvadUnitsPerInch, MidpointRounding.AwayFromZero);

    internal static long PvadToEmu(double units)
        => (long)Math.Round(units * EmuPerInch / PvadUnitsPerInch, MidpointRounding.AwayFromZero);

    internal static int PointsToHalfPoints(double points)
        => (int)Math.Round(points * 2.0, MidpointRounding.AwayFromZero);
}
