namespace Canvas.Pdf;

public interface IPdfColor
{
    string ToFillColorOperator();

    string ToStrokeColorOperator();
}
