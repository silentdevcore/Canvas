using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfTableFlowService : ITableFlowService
{
    public void ApplySimpleTable(object flowContext, object rows, object? options = null)
    {
        if (flowContext is not PXA.Pdf.PdfFlowContext flow)
        {
            throw new ArgumentException("Flow context must be PXA.Pdf.PdfFlowContext for PdfTableFlowService.", nameof(flowContext));
        }

        if (rows is not IReadOnlyList<IReadOnlyList<string>> tableRows)
        {
            throw new ArgumentException("Rows must be IReadOnlyList<IReadOnlyList<string>>.", nameof(rows));
        }

        if (options is null)
        {
            flow.AddSimpleTable(tableRows);
            return;
        }

        if (options is PXA.Pdf.PdfTableOptions tableOptions)
        {
            flow.AddSimpleTable(tableRows, tableOptions);
            return;
        }

        throw new ArgumentException("Options must be PXA.Pdf.PdfTableOptions when provided.", nameof(options));
    }
}
