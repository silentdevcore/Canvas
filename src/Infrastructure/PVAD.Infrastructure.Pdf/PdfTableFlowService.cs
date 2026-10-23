using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfTableFlowService : ITableFlowService
{
    public void ApplySimpleTable(object flowContext, object rows, object? options = null)
    {
        if (flowContext is not PVAD.Pdf.PdfFlowContext flow)
        {
            throw new ArgumentException("Flow context must be PVAD.Pdf.PdfFlowContext for PdfTableFlowService.", nameof(flowContext));
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

        if (options is PVAD.Pdf.PdfTableOptions tableOptions)
        {
            flow.AddSimpleTable(tableRows, tableOptions);
            return;
        }

        throw new ArgumentException("Options must be PVAD.Pdf.PdfTableOptions when provided.", nameof(options));
    }
}
