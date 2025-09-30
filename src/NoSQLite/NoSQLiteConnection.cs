using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;

namespace NoSQLite;

/// <summary>
/// Represents a connection to a NoSQLite database, providing methods to manage tables and documents.
/// </summary>
[Preserve(AllMembers = true)]
public sealed class NoSQLiteConnection : IDisposable
{
    private readonly bool disposeDb;

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
    /// <param name="databasePath">The path to the database file, or the path where the file will be created.</param>
    /// <param name="jsonOptions">The options used to serialize/deserialize JSON objects, or <c>null</c> for defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="databasePath"/> is <c>null</c>.</exception>
    public NoSQLiteConnection(string databasePath, JsonSerializerOptions? jsonOptions = null, bool wal = true)
    {
        ArgumentNullException.ThrowIfNull(databasePath, nameof(databasePath));
        disposeDb = true;

        var result = sqlite3_open(databasePath, out db);
        db.CheckResult(result, $"Could not open or create database file: {Path}");

        if (wal)
        {
            SetJournalMode();
        }

        Version = sqlite3_libversion().utf8_to_string();
        Name = System.IO.Path.GetFileName(databasePath);
        Path = databasePath;
        JsonOptions = jsonOptions;
        tables = [];
    }

    /// <summary>
    /// Sets the SQLite journal mode to Write-Ahead Logging (WAL).
    /// </summary>
    public void SetJournalMode()
    {
        sqlite3_exec(db, "PRAGMA journal_mode=WAL;");
    }

    /// <summary>
    /// Gets or sets the JSON serializer options used to serialize/deserialize the documents.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the database path used by this connection.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the SQLite library version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets all the tables that belong to this connection.
    /// </summary>
    public IReadOnlyCollection<NoSQLiteTable> Tables => [.. tables.Values];

    /// <summary>
    /// Gets a table with the specified name, or the default "documents" table if <paramref name="table"/> is <c>null</c>.
    /// If the table does not exist in the connection's cache, a new <see cref="NoSQLiteTable"/> is created.
    /// </summary>
    /// <param name="table">The name of the table to create or use, or <c>null</c> to use the default "documents" table.</param>
    /// <returns>The <see cref="NoSQLiteTable"/> instance for the specified table.</returns>
    public NoSQLiteTable GetTable(string? table = null)
    {
        const string docs = "documents";
        return tables.GetOrAdd(table ?? docs, static (table, connection) => new(table, connection), this);
    }

    /// <summary>
    /// Checks whether a table with the specified name exists in the database.
    /// </summary>
    /// <param name="table">The name of the table to check.</param>
    /// <returns><c>true</c> if the table exists; otherwise, <c>false</c>.</returns>
    public bool TableExists(string table)
    {
        using var stmt = new SQLiteStmt(db, $"""
            SELECT count(*) FROM "sqlite_master"
            WHERE type='table' AND name='{table}';
            """);

        stmt.Step();
        return stmt.ColumnInt(0) != 0;
    }

    /// <summary>
    /// Creates a document table with the specified name if it does not already exist.
    /// </summary>
    /// <param name="table">The name of the table to create.</param>
    /// <exception cref="Exception">Thrown if the table cannot be created.</exception>
    public void CreateTable(string table)
    {
        var result = sqlite3_exec(db, $"""
            CREATE TABLE IF NOT EXISTS "{table}" (
                "id"        TEXT NOT NULL UNIQUE,
                "json"      TEXT NOT NULL,
                PRIMARY KEY("id")
            );
            """);

        db.CheckResult(result, $"Could not create '{table}' database table");
    }

    /// <summary>
    /// Drops a table with the specified name if it exists. This will delete all indexes, views, etc.
    /// </summary>
    /// <param name="table">The name of the table to drop.</param>
    /// <exception cref="Exception">Thrown if the table cannot be dropped.</exception>
    public void DropTable(string table)
    {
        var result = sqlite3_exec(db, $"""
            DROP TABLE IF EXISTS "{table}";
            """);

        db.CheckResult(result, $"Could not drop '{table}' database table");
    }

    /// <summary>
    /// Drops the table and then creates it again. This will delete all indexes, views, etc.
    /// </summary>
    /// <param name="table">The name of the table to drop and recreate.</param>
    /// <remarks>See <see href="https://sqlite.org/lang_droptable.html"/> for more info.</remarks>
    public void DropAndCreateTable(string table)
    {
        DropTable(table);
        CreateTable(table);
    }

    /// <summary>
    /// Deletes all rows from a table.
    /// </summary>
    /// <param name="table">The name of the table to clear.</param>
    public void Clear(string table)
    {
        sqlite3_exec(db, $"""DELETE FROM "{table}";""");
    }

    /// <summary>
    /// Executes a write-ahead log (WAL) checkpoint for the database.
    /// </summary>
    /// <remarks>See <see href="https://sqlite.org/c3ref/wal_checkpoint.html"/> for more info.</remarks>
    public void Checkpoint()
    {
        sqlite3_wal_checkpoint(db, Name);
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

        for (int i = 0; i < length; i++) buffer[i].Dispose();

        ArrayPool<NoSQLiteTable>.Shared.Return(buffer, true);

        if (disposeDb)
        {
            sqlite3_close_v2(db);
            db.Dispose();
        }
    }
}
