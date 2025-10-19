using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;

namespace NoSQLite;

/// <summary>
/// Represents a connection to a NoSQLite database, providing methods to manage tables and documents.
/// </summary>
[Preserve(AllMembers = true)]
public sealed partial class NoSQLiteConnection : IDisposable
{
    private readonly Lazy<SQLiteStmt> tableExistsStmt;

    /// <summary>
    /// The collection of tables managed by this connection.
    /// </summary>
    internal readonly ConcurrentDictionary<string, NoSQLiteTable> tables;

    /// <summary>
    /// The underlying SQLite database handle.
    /// </summary>
    internal readonly sqlite3 db;

    /// <summary>
    /// Indicates whether the connection is open.
    /// </summary>
    internal bool open = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoSQLiteConnection"/> class.
    /// </summary>
    /// <param name="db">The sqlite3 database.</param>
    /// <param name="jsonOptions">The options used to serialize/deserialize JSON objects, or <c>null</c> for defaults.</param>
    public NoSQLiteConnection(sqlite3 db, JsonSerializerOptions? jsonOptions = null)
    {
        this.db = db;
        tables = [];
        Version = sqlite3_libversion().utf8_to_string();
        JsonOptions = jsonOptions;

        tableExistsStmt = new(() => new SQLiteStmt(this.db, JsonOptions, """
            SELECT name FROM "sqlite_master"
            WHERE type='table' AND name = ?;
            """u8));
    }

    /// <summary>
    /// Gets the JSON serializer options used to serialize/deserialize the documents.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; }

    /// <summary>
    /// Gets the SQLite library version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets all the tables that belong to this connection.
    /// </summary>
    public IEnumerable<NoSQLiteTable> Tables => tables.Values;

    /// <summary>
    /// Gets a table with the specified name, or the default "documents" table if <paramref name="table"/> is <c>null</c>.
    /// If the table does not exist in the connection's cache, a new <see cref="NoSQLiteTable"/> is created.
    /// </summary>
    /// <param name="table">The name of the table to use and create if it does not exist.</param>
    /// <returns>The <see cref="NoSQLiteTable"/> instance for the specified table.</returns>
    public NoSQLiteTable GetTable(string table)
    {
        return tables.GetOrAdd(table, static (table, connection) => new(table, connection), this);
    }

    /// <summary>
    /// Checks whether a table with the specified name exists in the database.
    /// </summary>
    /// <param name="table">The name of the table to check.</param>
    /// <returns><c>true</c> if the table exists; otherwise, <c>false</c>.</returns>
    public bool TableExists(string table)
    {
        var stmt = tableExistsStmt.Value;
        return stmt.Execute(b => b.Text(1, table), static r => r.Result is SQLITE_ROW, shouldThrow: false);
    }

    /// <summary>
    /// Creates a document table with the specified name if it does not already exist.
    /// </summary>
    /// <param name="table">The name of the table to create.</param>
    /// <exception cref="Exception">Thrown if the table cannot be created.</exception>
    public void CreateTable(string table)
    {
        using var stmt = new SQLiteStmt(db, JsonOptions, $"""
            CREATE TABLE IF NOT EXISTS "{table}" (
                "documents" JSON NOT NULL
            );
            """);
        stmt.Execute(b => b.Text(1, table));
    }

    /// <summary>
    /// Drops a table with the specified name if it exists. This will delete all indexes, views, etc.
    /// </summary>
    /// <param name="table">The name of the table to drop.</param>
    /// <exception cref="NoSQLiteException">Thrown if the table cannot be dropped.</exception>
    public void DropTable(string table)
    {
        using var stmt = new SQLiteStmt(db, JsonOptions, $"""
            DROP TABLE IF EXISTS "{table}";
        """);
        stmt.Execute(b => b.Text(1, table));
    }

    /// <summary>
    /// Releases all resources used by the <see cref="NoSQLiteConnection"/> and closes the underlying database connection.
    /// </summary>
    /// <remarks>
    /// This will close and dispose the underlying database connection and all associated tables.
    /// </remarks>
    public void Dispose()
    {
        if (!open) return;

        open = false;

        var length = tables.Count;
        var buffer = ArrayPool<NoSQLiteTable>.Shared.Rent(length);
        tables.Values.CopyTo(buffer, 0);
        tables.Clear();

        for (int i = 0; i < length; i++) buffer[i].Dispose();

        ArrayPool<NoSQLiteTable>.Shared.Return(buffer, true);

        if (tableExistsStmt.IsValueCreated) tableExistsStmt.Value.Dispose();
    }
}
