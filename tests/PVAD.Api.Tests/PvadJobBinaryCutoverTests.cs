using System.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadJobBinaryCutoverTests
{
    [BinaryCutoverFact]
    public async Task Pxa_worker_process_is_killed_and_pvad_resumes_its_persisted_job()
    {
        var legacy = Environment.GetEnvironmentVariable("PVAD_LEGACY_JOB_HARNESS");
        var current = Environment.GetEnvironmentVariable("PVAD_CURRENT_JOB_HARNESS");
        Assert.True(File.Exists(legacy), "A compiled legacy harness is required.");
        Assert.True(File.Exists(current), "A compiled current harness is required.");
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("pvad_cutover_test").Build();
        await postgres.StartAsync();
        var root = Path.Combine(Path.GetTempPath(), "pvad-binary-cutover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process Start(string binary, string operation)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            info.ArgumentList.Add(binary); info.ArgumentList.Add(operation);
            info.Environment["PVAD_CUTOVER_TEST_ALLOW"] = "1";
            info.Environment["PVAD_CUTOVER_CONNECTION"] = postgres.GetConnectionString();
            info.Environment["PVAD_CUTOVER_OBJECT_ROOT"] = root;
            return Process.Start(info) ?? throw new InvalidOperationException("Cannot start isolated harness.");
        }
        async Task RunAsync(string binary, string operation)
        {
            using var process = Start(binary, operation);
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                await process.WaitForExitAsync(timeout.Token);
                Assert.True(process.ExitCode == 0, $"Cutover operation {operation} failed: {await errors}");
                Assert.False(string.IsNullOrWhiteSpace(await output));
            }
            finally
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                await output; await errors;
            }
        }
        try
        {
            await RunAsync(legacy!, "seed");
            var schemaBefore = await SnapshotSchemaAsync(postgres.GetConnectionString());
            using (var interrupted = Start(legacy!, "pause"))
            {
                var errors = interrupted.StandardError.ReadToEndAsync();
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    string? line;
                    do { line = await interrupted.StandardOutput.ReadLineAsync(timeout.Token); }
                    while (line is not null && line != "CLAIMED");
                    Assert.Equal("CLAIMED", line);
                    interrupted.Kill(entireProcessTree: true);
                    await interrupted.WaitForExitAsync(timeout.Token);
                }
                finally
                {
                    if (!interrupted.HasExited) { interrupted.Kill(entireProcessTree: true); await interrupted.WaitForExitAsync(); }
                    await errors;
                }
            }
            // Deliberate test clock advance, only after the old OS process is gone.
            await RunAsync(current!, "expire");
            await RunAsync(current!, "cancel");
            await RunAsync(current!, "process");
            await RunAsync(current!, "verify");
            await RunAsync(legacy!, "verify");
            Assert.Equal(schemaBefore, await SnapshotSchemaAsync(postgres.GetConnectionString()));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task<string[]> SnapshotSchemaAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT table_schema, table_name, column_name, data_type, is_nullable,
                   COALESCE(column_default, '')
            FROM information_schema.columns
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY table_schema, table_name, ordinal_position
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
            columns.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)));
        Assert.NotEmpty(columns);
        return columns.ToArray();
    }
}

public sealed class BinaryCutoverFactAttribute : FactAttribute
{
    public BinaryCutoverFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PVAD_RUN_BINARY_CUTOVER_TESTS") != "1")
            Skip = "Set PVAD_RUN_BINARY_CUTOVER_TESTS=1 and both compiled harness paths to run the paired-binary test.";
    }
}
