namespace Canvas.Core.Abstractions;

public interface ITextMeasurer
{
    double MeasureTextWidth(string text, double fontSize, string fontKey);
}
