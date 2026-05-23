namespace Canvas.Core.Abstractions;

public interface IDocumentRenderer
{
    byte[] Render(object documentModel);
}
