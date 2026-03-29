using Microsoft.Data.Sqlite;
using System.Data;

namespace OnFlight.Core.Data;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public (IDbConnection connection, IDbTransaction transaction) CreateTransaction()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var transaction = connection.BeginTransaction();
        return (connection, transaction);
    }
}
