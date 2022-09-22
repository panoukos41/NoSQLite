using SQLitePCL;
using System.Runtime.CompilerServices;
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

    private readonly sqlite3 db;
    private bool open = true;

    /// <summary>
    /// Initialize a new instance of <see cref="NoSQLiteConnection"/> class.
    /// </summary>
    /// <param name="databasePath">The path pointing to the database file or the path it will create file.</param>
    /// <param name="jsonOptions">The options that will be used to serialize/deserialize the json objects.</param>
    public NoSQLiteConnection(string databasePath, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(databasePath, nameof(databasePath));

        Path = databasePath;
        Version = sqlite3_libversion().utf8_to_string();
        JsonOptions = jsonOptions;

        var result = sqlite3_open(Path, out db);
        CheckResult(result, $"Could not open or create database file: {Path}");

        result = CreateTable();
        CheckResult(result, $"Could not create 'documents' database table");

        SetJournalMode();

        Indexes = new(db);
    }

    private void SetJournalMode() =>
        sqlite3_exec(db, "PRAGMA journal_mode=WAL;");

    private int CreateTable() => sqlite3_exec(db, """
        CREATE TABLE IF NOT EXISTS documents (
            "id"        TEXT NOT NULL UNIQUE,
            "json"      TEXT NOT NULL,
            PRIMARY KEY("id")
        );
        """);

    private void CheckResult(int result, [InterpolatedStringHandlerArgument("result")] ref ConditionalInterpolation message)
    {
        if (message.ShouldThrow)
        {
            throw new NoSQLiteException($"{message.ToString()}. SQLite info, code: {result}, message: {sqlite3_errmsg(db).utf8_to_string()}");
        }
    }

    /// <summary>
    /// Gets the database path used by this connection.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the SQLite library version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the JSON serializer options used to serialzie/deserialize the JSON.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>
    /// Returnes a class that manages the Indexes.
    /// </summary>
    public NoSQLiteIndexes Indexes { get; }

    ///// <summary>
    ///// Returnes a class that manages the Views.
    ///// </summary>
    //public NoSQLiteViews Views { get; }

    /// <summary>
    /// Check wheter an id exists or not.
    /// </summary>
    /// <param name="id">The id to search for.</param>
    /// <returns>True when the id exists otherwise false.</returns>
    public bool Exists(string id)
    {
        const string sql = """
            SELECT count(*) FROM documents
            WHERE id is ?;
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, id);
        stmt.Step();

        var value = stmt.ColumnInt(0);
        return value == 1;
    }

    /// <summary>
    /// Get a byte array for the provided id or null.
    /// </summary>
    /// <param name="id">The id to search for.</param>
    /// <returns>A byte array or null.</returns>
    public byte[]? GetBytes(string id)
    {
        const string sql = """
            SELECT json
            FROM documents
            WHERE id is ?;
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, id);
        var result = stmt.Step();

        return result switch
        {
            SQLITE_ROW => stmt.ColumnBlob(0).ToArray(),
            _ => null
        };
    }

    // todo: GET: throw if not exists.
    public List<byte[]> GetBytes(IEnumerable<string> ids)
    {
        const string sql = """
            SELECT json
            FROM documents
            WHERE id is ?;
            """;

        var items = new List<byte[]>();
        using var stmt = new SqliteStatement(db, sql);
        // consider not using a command here to provide yield return.

        foreach (var id in ids)
        {
            stmt.BindText(1, id);
            stmt.Step();
            var bytes = stmt.ColumnBlob(0);
            items.Add(bytes.ToArray());
            stmt.Reset();
        }

        return items;
    }

    public void InsertBytes(string id, byte[] obj)
    {
        const string sql = """
            REPLACE INTO documents
            VALUES (?, json(?));
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, id);
        stmt.BindText(2, obj);
        stmt.Step();
    }

    public void InsertBytes(IDictionary<string, byte[]> keyValuePairs)
    {
        const string sql = """
            REPLACE INTO documents
            VALUES (?, json(?));
            """;

        using var stmt = new SqliteStatement(db, sql);
        foreach (var (id, obj) in keyValuePairs)
        {
            stmt.BindText(1, id);
            stmt.BindText(2, obj);
            stmt.Step();
            stmt.Reset();
        }
    }

    /// <summary>
    /// Get an object for the provided id or null.
    /// </summary>
    /// <typeparam name="T">The type the object will deserialize to.</typeparam>
    /// <param name="id">The id to search for.</param>
    /// <returns>An instance of <typeparamref name="T"/> or null.</returns>
    public T? Get<T>(string id)
    {
        const string sql = """
            SELECT json
            FROM documents
            WHERE id is ?;
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, id);
        var result = stmt.Step();

        return result switch
        {
            SQLITE_ROW => stmt.ColumnDeserialize<T>(0),
            _ => default
        };
    }

    public List<T> Get<T>(IEnumerable<string> ids)
    {
        const string sql = """
            SELECT json
            FROM documents
            WHERE id is ?;
            """;

        var items = new List<T>();
        using var stmt = new SqliteStatement(db, sql);
        // consider not using a command here to provide yield return.

        foreach (var id in ids)
        {
            stmt.BindText(1, id);
            stmt.Step();
            var bytes = stmt.ColumnBlob(0);
            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
            items.Add(value);
            stmt.Reset();
        }

        return items;
    }

    public void Insert<T>(string id, T obj)
    {
        const string sql = """
            REPLACE INTO documents
            VALUES (?, json(?));
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, id);
        stmt.BindText(2, JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions));
        var result = stmt.Step();

        CheckResult(result, $"Could not Insert ({id})");
    }

    public void Insert<T>(IDictionary<string, T> keyValuePairs)
    {
        const string sql = """
            REPLACE INTO documents
            VALUES (?, json(?));
            """;

        using var stmt = new SqliteStatement(db, sql);
        foreach (var (id, obj) in keyValuePairs)
        {
            stmt.BindText(1, id);
            stmt.BindText(2, JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions));
            stmt.Step();
            stmt.Reset();
        }
    }

    /// <summary>
    /// Deletes the specified id from the database.
    /// </summary>
    /// <param name="id">The id to delete.</param>
    public void Remove(string id)
    {
        const string sql = """
            DELETE FROM documents
            WHERE id is ?;
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, id);
        stmt.Step();
    }

    /// <summary>
    /// Deletes the specified ids from the database.
    /// </summary>
    /// <param name="ids">The ids to delete.</param>
    public void Remove(IEnumerable<string> ids)
    {
        const string sql = """
            DELETE FROM documents
            WHERE id is ?;
            """;

        using var stmt = new SqliteStatement(db, sql);
        foreach (var id in ids)
        {
            stmt.BindText(1, id);
            stmt.Step();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This will close and dispose the underlying database connection.
    /// </remarks>
    public void Dispose()
    {
        if (!open) return;

        open = false;
        sqlite3_close_v2(db);
        db.Dispose();
    }
}
