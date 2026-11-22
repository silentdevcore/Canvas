namespace PVAD.Migration.Report;

/// <summary>Converts a report definition into an editable PVAD design and migration diagnostics.</summary>
/// <remarks>Built-in converters require Report Designer Migration rights for the selected provider.
/// This contract does not execute report scripts, query data sources or render a PDF.</remarks>
public interface IReportMigration
{
    /// <summary>Imports the selected provider's report definition.</summary>
    /// <param name="source">The report's textual definition, not a ZIP package or rendered PDF.</param>
    /// <returns>The editable design and diagnostics describing omissions or approximations.</returns>
    ReportMigrationResult Convert(string source);
}
