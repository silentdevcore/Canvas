using Canvas.Importer.Fonts;

namespace Canvas.Importer.Graphics;

public sealed record GraphicsState
{
    public PdfMatrix Transform { get; init; } = PdfMatrix.Identity;
    public PdfFontResource? CurrentFont { get; init; }
    public double FontSize { get; init; }
    public PdfColor FillColor { get; init; } = PdfColor.Black;
    public PdfColor StrokeColor { get; init; } = PdfColor.Black;
    public double LineWidth { get; init; } = 1;
    public double CharacterSpacing { get; init; }
    public double WordSpacing { get; init; }
    public double TextLeading { get; init; }
    public PdfMatrix TextMatrix { get; init; } = PdfMatrix.Identity;
    public PdfMatrix TextLineMatrix { get; init; } = PdfMatrix.Identity;
}

public sealed class GraphicsStateStack
{
    private readonly Stack<GraphicsState> _states = new();

    public GraphicsState Current { get; private set; } = new();

    public void Save() => _states.Push(Current);

    public void Restore()
    {
        if (_states.TryPop(out var state))
        {
            Current = state;
        }
    }

    public void Update(Func<GraphicsState, GraphicsState> update) => Current = update(Current);
}
