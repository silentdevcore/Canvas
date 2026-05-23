namespace Canvas.Core.Abstractions;

public interface ITableFlowService
{
    void ApplySimpleTable(object flowContext, object rows, object? options = null);
}
