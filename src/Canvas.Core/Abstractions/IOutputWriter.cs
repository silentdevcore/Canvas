namespace Canvas.Core.Abstractions;

public interface IOutputWriter
{
    void Write(string path, byte[] data);
}
