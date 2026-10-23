namespace PVAD.Migration.Report;

public interface IReportMigration
{
    ReportMigrationResult Convert(string source);
}
