using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OnFlight.Contracts.Schema;

namespace OnFlight.Core.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer>? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger ?? NullLogger<DatabaseInitializer>.Instance;
    }

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        _logger.LogInformation("Database initialized at {DbPath}", _connectionString);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {SchemaVersion.TableName} (
                version INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS todo_lists (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentItemId TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS todo_items (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Description TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                ParentListId TEXT NOT NULL,
                SubListId TEXT,
                NodeType INTEGER NOT NULL DEFAULT 0,
                FlowConfigJson TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ParentListId) REFERENCES todo_lists(Id)
            );

            CREATE TABLE IF NOT EXISTS operation_logs (
                Id TEXT PRIMARY KEY,
                ListId TEXT NOT NULL,
                OperationType INTEGER NOT NULL,
                Detail TEXT,
                Timestamp TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                FOREIGN KEY (ListId) REFERENCES todo_lists(Id)
            );

            CREATE INDEX IF NOT EXISTS idx_todo_items_parent ON todo_items(ParentListId);
            CREATE INDEX IF NOT EXISTS idx_todo_items_sort ON todo_items(ParentListId, SortOrder);
            CREATE TABLE IF NOT EXISTS running_instances (
                Id TEXT PRIMARY KEY,
                SourceListId TEXT NOT NULL,
                ListName TEXT NOT NULL,
                StateJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                DeviceId TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_operation_logs_list ON operation_logs(ListId);
            CREATE INDEX IF NOT EXISTS idx_operation_logs_time ON operation_logs(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_running_instances_source ON running_instances(SourceListId);
        ";
        cmd.ExecuteNonQuery();

        using var verCmd = connection.CreateCommand();
        verCmd.CommandText = $"SELECT COUNT(*) FROM {SchemaVersion.TableName}";
        var count = (long)verCmd.ExecuteScalar()!;
        if (count == 0)
        {
            using var insCmd = connection.CreateCommand();
            insCmd.CommandText = $"INSERT INTO {SchemaVersion.TableName} (version) VALUES ({SchemaVersion.Current})";
            insCmd.ExecuteNonQuery();
        }
    }

    public void ClearAllData()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM running_instances;
            DELETE FROM operation_logs;
            DELETE FROM todo_items;
            DELETE FROM todo_lists;
        ";
        cmd.ExecuteNonQuery();
        _logger.LogInformation("All data cleared");
    }
}
