using SQLitePCL;
using System.Buffers;
using System.Text.Json;

namespace NoSQLite;

using static SQLitePCL.raw;

/// <summary>
/// 
/// </summary>
[Preserve(AllMembers = true)]
public sealed class NoSQLiteConnection : IDisposable
{
    static NoSQLiteConnection()
    {
        Batteries.Init();
    }

    internal readonly Dictionary<string, NoSQLiteTable> tables;
    internal readonly sqlite3 db;
    internal bool open = true;

    /// <summary>
    /// Initialize a new instance of <see cref="NoSQLiteConnection"/> class.
    /// </summary>
    /// <param name="databasePath">The path pointing to the database file or the path it will create the file at.</param>
    /// <param name="jsonOptions">The options that will be used to serialize/deserialize the json objects.</param>
    public NoSQLiteConnection(string databasePath, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(databasePath, nameof(databasePath));

        var result = sqlite3_open(databasePath, out db);
        db.CheckResult(result, $"Could not open or create database file: {Path}");

        SetJournalMode();

        Version = sqlite3_libversion().utf8_to_string();
        Name = System.IO.Path.GetFileName(databasePath);
        Path = databasePath;
        JsonOptions = jsonOptions;
        tables = new();
    }

    private void SetJournalMode() =>
        sqlite3_exec(db, "PRAGMA journal_mode=WAL;");

    /// <summary>
    /// All the tables that belong to this Connection.
    /// </summary>
    public IReadOnlyCollection<NoSQLiteTable> Tables => tables.Values;

    /// <summary>
    /// The database name.
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
    /// Gets or Sets the JSON serializer options used to serialzie/deserialize the documents.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>
    /// todo: Summary
    /// </summary>
    /// <param name="table">The name of the table to create/use or null to use the default documents table.</param>
    /// <returns></returns>
    public NoSQLiteTable GetTable(string? table = null)
    {
        const string docs = "documents";
        return tables.TryGetValue(table ?? docs, out var t) ? t : new(table ?? docs, this);
    }

    /// <summary>
    /// Create a <b>document</b> table if it does not exist with the specified name.
    /// </summary>
    /// <param name="table"></param>
    public void CreateTable(string table)
    {
        var result = sqlite3_exec(db, $"""
            CREATE TABLE IF NOT EXISTS '{table}' (
                "id"        TEXT NOT NULL UNIQUE,
                "json"      TEXT NOT NULL,
                PRIMARY KEY("id")
            );
            """);

        db.CheckResult(result, $"Could not create '{table}' database table");
    }

    /// <summary>
    /// Deletes all rows from a table.
    /// </summary>
    public void Clear(string table)
    {
        sqlite3_exec(db, $"""DELETE FROM "{table}";""");
    }

    /// <summary>
    /// Drops the table and then create it again. This will delete all indexes views etc.
    /// </summary>
    /// <remarks>See <see href="https://sqlite.org/lang_droptable.html"/> for more info.</remarks>
    public void DropAndCreate(string table)
    {
        sqlite3_exec(db, $"""DROP TABLE IF EXISTS "{table}";""");
        CreateTable(table);
    }

    /// <summary>
    /// Execute wal_checkpoint.
    /// </summary>
    /// <remarks>See <see href="https://sqlite.org/c3ref/wal_checkpoint.html"/> for more info.</remarks>
    public void Checkpoint()
    {
        sqlite3_wal_checkpoint(db, Name);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This will close and dispose the underlying database connection.
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

        sqlite3_close_v2(db);
        db.Dispose();
    }
}
