using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Adapts string-table data to native PDF flow layout.</summary>
public sealed class PdfTableFlowService : ITableFlowService
{
    /// <summary>Adds rows to a flow context, allowing the native engine to paginate the table.</summary>
    /// <param name="flowContext">A <see cref="PVAD.Pdf.PdfFlowContext"/> attached to the destination document.</param>
    /// <param name="rows">An <see cref="IReadOnlyList{T}"/> of read-only string rows.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfTableOptions"/>, or null for defaults.</param>
    /// <exception cref="ArgumentException">The context, row collection or non-null options have the wrong type.</exception>
    /// <remarks>Underlying page creation and drawing retain native PDF license checks.</remarks>
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
