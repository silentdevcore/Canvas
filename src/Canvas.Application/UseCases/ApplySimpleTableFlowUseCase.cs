using Canvas.Core.Abstractions;

namespace Canvas.Application.UseCases;

public sealed class ApplySimpleTableFlowUseCase
{
    private readonly ITableFlowService _service;

    public ApplySimpleTableFlowUseCase(ITableFlowService service)
    {
        _service = service;
    }

    public void Execute(object flowContext, object rows, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(flowContext);
        ArgumentNullException.ThrowIfNull(rows);

        _service.ApplySimpleTable(flowContext, rows, options);
    }
}
