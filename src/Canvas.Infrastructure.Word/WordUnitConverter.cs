namespace Canvas.Infrastructure.Word;

internal static class WordUnitConverter
{
    // Canvas coordinates are aligned with the PDF model (72 units per inch).
    private const double CanvasUnitsPerInch = 72.0;
    private const double TwipsPerInch = 1440.0;
    private const double EmuPerInch = 914400.0;

    internal static int CanvasToTwips(double units)
        => (int)Math.Round(units * TwipsPerInch / CanvasUnitsPerInch, MidpointRounding.AwayFromZero);

    internal static long CanvasToEmu(double units)
        => (long)Math.Round(units * EmuPerInch / CanvasUnitsPerInch, MidpointRounding.AwayFromZero);

    internal static int PointsToHalfPoints(double points)
        => (int)Math.Round(points * 2.0, MidpointRounding.AwayFromZero);
}
