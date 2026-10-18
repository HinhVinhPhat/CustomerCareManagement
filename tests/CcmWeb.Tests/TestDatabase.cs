using System.Text;
using Microsoft.Data.SqlClient;

namespace CcmWeb.Tests;

/// <summary>
/// Rebuilds a separate test database from the real scripts in <c>database/</c> with the database
/// name replaced. The tests therefore exercise the same tables, views, procedures and triggers as
/// the application, while the live CustomerCareDB is never written to by a test.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    private const string LiveDatabaseName = "CustomerCareDB";
    private const string TestDatabaseName = "CustomerCareDB_Test";

    /// <summary>SQL Server used by the tests; override with the CCM_TEST_SERVER variable.</summary>
    public static string Server { get; } =
        Environment.GetEnvironmentVariable("CCM_TEST_SERVER") ?? @".\SQLEXPRESS";

    public static string ConnectionString { get; } = BuildConnectionString(TestDatabaseName);

    private static string BuildConnectionString(string database) =>
        $"Server={Server};Database={database};Integrated Security=True;TrustServerCertificate=True";

    public async Task InitializeAsync()
    {
        var databaseFolder = Path.Combine(RepositoryRoot(), "database");

        foreach (var file in new[]
                 {
                     "01_CreateTables.sql",
                     "02_Function_View_Proc_Trigger.sql",
                     "03_SampleData.sql"
                 })
        {
            var script = await File.ReadAllTextAsync(Path.Combine(databaseFolder, file));

            // File 01 drops and recreates the database, so every test run starts from a clean one.
            await RunAsync(script.Replace(LiveDatabaseName, TestDatabaseName, StringComparison.Ordinal));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task RunAsync(string script)
    {
        await using var connection = new SqlConnection(BuildConnectionString("master"));
        await connection.OpenAsync();

        foreach (var batch in Batches(script))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Splits a script at the GO batch separators, the way sqlcmd does.</summary>
    private static IEnumerable<string> Batches(string script)
    {
        var batch = new StringBuilder();

        foreach (var line in script.Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.ToString().Trim().Length > 0)
                {
                    yield return batch.ToString();
                }

                batch.Clear();
                continue;
            }

            batch.AppendLine(line);
        }

        if (batch.ToString().Trim().Length > 0)
        {
            yield return batch.ToString();
        }
    }

    /// <summary>
    /// The repository root, found by walking up from the test binary. Exposed so the resource tests
    /// can read the same files the application ships.
    /// </summary>
    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "database", "01_CreateTables.sql")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root (the database folder) was not found.");
    }
}

[CollectionDefinition("database")]
public sealed class DatabaseCollection : ICollectionFixture<TestDatabase>
{
}
