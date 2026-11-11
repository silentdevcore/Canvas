using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Removes the job diagnostics copy that an unreleased revision of RetireFormerNameData created.
    /// That copy was outside job retention; its content is intentionally not recreated on rollback.
    /// </summary>
    public partial class DropFormerNameJobBackup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS administration.former_name_job_backup;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
