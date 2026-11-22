namespace PVAD.Migration.Abstractions;

/// <summary>Converts source code to PVAD code and reports operations requiring review.</summary>
/// <remarks>The built-in implementations require the native migration product and selected provider
/// grant. An interface reference, provider listing or result object does not confer those rights.</remarks>
public interface ISourceMigration
{
    /// <summary>Rewrites source without executing it or producing a rendered document.</summary>
    /// <param name="sourceCode">Source text in the selected provider's supported input language.</param>
    /// <returns>Generated code and migration diagnostics; review them before executing the result.</returns>
    MigrationResult Migrate(string sourceCode);
}
