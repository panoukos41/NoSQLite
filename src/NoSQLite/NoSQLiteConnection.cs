using SQLitePCL;
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

    private readonly List<IDisposable> disposables = new();
    private readonly sqlite3 db;
    private bool open = true;

    /// <summary>
    /// Initialize a new instance of <see cref="NoSQLiteConnection"/> class.
    /// </summary>
    /// <param name="databasePath">The path pointing to the database file or the path it will create the file at.</param>
    /// <param name="jsonOptions">The options that will be used to serialize/deserialize the json objects.</param>
    public NoSQLiteConnection(string databasePath, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(databasePath, nameof(databasePath));

        Path = databasePath;
        Version = sqlite3_libversion().utf8_to_string();
        JsonOptions = jsonOptions;

        var result = sqlite3_open(Path, out db);
        db.CheckResult(result, $"Could not open or create database file: {Path}");

        result = CreateTable();
        db.CheckResult(result, $"Could not create '{Table}' database table");

        SetJournalMode();

        #region Lazy statment initialization

        existsStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                SELECT count(*) FROM {Table}
                WHERE id is ?;
                """);

            disposables.Add(stmt);
            return stmt;
        });

        findStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                SELECT json
                FROM {Table}
                WHERE id is ?;
                """);

            disposables.Add(stmt);
            return stmt;
        });

        insertStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                REPLACE INTO {Table}
                VALUES (?, json(?));
                """);

            disposables.Add(stmt);
            return stmt;
        });

        removeStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                DELETE FROM {Table}
                WHERE id is ?;
                """);

            disposables.Add(stmt);
            return stmt;
        });
        #endregion
    }

    private int CreateTable() =>
        sqlite3_exec(db, $"""
            CREATE TABLE IF NOT EXISTS {Table} (
                "id"        TEXT NOT NULL UNIQUE,
                "json"      TEXT NOT NULL,
                PRIMARY KEY("id")
            );
            """);

    private void SetJournalMode() =>
        sqlite3_exec(db, "PRAGMA journal_mode=WAL;");

    /// <summary>
    /// The name of the table this connection will use.
    /// </summary>
    public string Table { get; } = "documents";

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
    /// Deletes all rows from a table.
    /// </summary>
    public void Clear()
    {
        sqlite3_exec(db, $"DELETE FROM {Table};");
    }

    /// <summary>
    /// Drops the table and then Creates it again. This will delete all indexes views etc.
    /// </summary>
    /// <remarks>See <see href="https://sqlite.org/lang_droptable.html"/> for more info.</remarks>
    public void DropAndCreate()
    {
        sqlite3_exec(db, $"DROP TABLE IF EXISTS {Table};");
        var result = CreateTable();

        db.CheckResult(result, $"Could not create '{Table}' database table");
    }

    public void Checkpoint()
    {
        sqlite3_wal_checkpoint(db, null);
    }

    #region Find

    private readonly Lazy<SQLiteStmt> existsStmt;

    /// <summary>
    /// Check wheter a document exists or not.
    /// </summary>
    /// <param name="id">The id to search for.</param>
    /// <returns>True when the id exists otherwise false.</returns>
    public bool Exists(string id)
    {
        var stmt = existsStmt.Value;
        lock (existsStmt)
        {
            stmt.BindText(1, id);
            stmt.Step();

            var value = stmt.ColumnInt(0);
            stmt.Reset();
            return value == 1;
        }
    }

    private readonly Lazy<SQLiteStmt> findStmt;

    /// <summary>
    /// Get an object for the provided id or null.
    /// </summary>
    /// <typeparam name="T">The type the object will deserialize to.</typeparam>
    /// <param name="id">The id to search for.</param>
    /// <returns>An instance of <typeparamref name="T"/> or null.</returns>
    public T? Find<T>(string id)
    {
        var stmt = findStmt.Value;
        lock (findStmt)
        {
            stmt.BindText(1, id);
            var result = stmt.Step();

            if (result is not SQLITE_ROW)
            {
                stmt.Reset();
                return default;
            }

            var value = stmt.ColumnDeserialize<T>(0, JsonOptions);
            stmt.Reset();
            return value;
        }
    }

    /// <summary>
    /// Get an object for each one of the provided ids.
    /// </summary>
    /// <typeparam name="T">The type the objects will deserialize to.</typeparam>
    /// <param name="ids">The ids to search for.</param>
    /// <param name="throwIfNotFound">True to ignore missing ids</param>
    /// <returns>A list of <typeparamref name="T"/> objects.</returns>
    /// <exception cref="KeyNotFoundException">When <paramref name="throwIfNotFound"/> is true and a key is not found.</exception>
    public IEnumerable<T> Find<T>(IEnumerable<string> ids, bool throwIfNotFound = true)
    {
        foreach (var id in ids)
        {
            var doc = Find<T>(id);

            Throw.KeyNotFound(throwIfNotFound && doc is null, $"Could not locate the Id '{id}'");

            yield return doc!;
        }
    }

    /// <summary>
    /// Get a byte array for the provided id or null.
    /// </summary>
    /// <param name="id">The id to search for.</param>
    /// <returns>A byte array or null.</returns>
    public byte[]? FindBytes(string id)
    {
        var stmt = findStmt.Value;
        lock (findStmt)
        {
            stmt.BindText(1, id);
            var result = stmt.Step();

            if (result is not SQLITE_ROW)
            {
                stmt.Reset();
                return default;
            }

            var bytes = stmt.ColumnBlob(0).ToArray();
            stmt.Reset();
            return bytes;
        }
    }

    /// <summary>
    /// Get a byte array for each one of the provided ids.
    /// </summary>
    /// <param name="ids">The ids to search for.</param>
    /// <param name="throwIfNotFound">True to ignore missing ids</param>
    /// <returns>A list of byte arrays.</returns>
    /// <exception cref="KeyNotFoundException">When <paramref name="throwIfNotFound"/> is true and a key is not found.</exception>
    public IEnumerable<byte[]> FindBytes(IEnumerable<string> ids, bool throwIfNotFound = true)
    {
        foreach (var id in ids)
        {
            var bytes = FindBytes(id);

            Throw.KeyNotFound(throwIfNotFound && bytes is null, $"Could not locate the Id '{id}'");

            yield return bytes!;
        }
    }

    #endregion

    #region Insert

    private readonly Lazy<SQLiteStmt> insertStmt;

    public void Insert<T>(string id, T obj) =>
        InsertBytes(id, JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions));

    public void Insert<T>(IDictionary<string, T> keyValuePairs)
    {
        using var transaction = new SQLiteTransaction(db);
        foreach (var (id, obj) in keyValuePairs)
        {
            Insert(id, obj);
        }
    }

    public void InsertBytes(string id, byte[] obj)
    {
        var stmt = insertStmt.Value;

        lock (insertStmt)
        {
            stmt.BindText(1, id);
            stmt.BindText(2, obj);
            var result = stmt.Step();

            db.CheckResult(result, $"Could not Insert ({id})");
            stmt.Reset();
        }
    }

    public void InsertBytes(IDictionary<string, byte[]> keyValuePairs)
    {
        foreach (var (id, obj) in keyValuePairs)
        {
            InsertBytes(id, obj);
        }
    }

    #endregion

    #region Remove

    private readonly Lazy<SQLiteStmt> removeStmt;

    /// <summary>
    /// Deletes the specified id from the database.
    /// </summary>
    /// <param name="id">The id to delete.</param>
    public void Remove(string id)
    {
        var stmt = removeStmt.Value;

        lock (removeStmt)
        {
            stmt.BindText(1, id);
            stmt.Step();
            stmt.Reset();
        }
    }

    /// <summary>
    /// Deletes the specified ids from the database.
    /// </summary>
    /// <param name="ids">The ids to delete.</param>
    public void Remove(IEnumerable<string> ids)
    {
        using var transaction = new SQLiteTransaction(db);
        foreach (var id in ids)
        {
            Remove(id);
        }
    }

    #endregion

    #region Indexing

    /// <summary>
    /// Check whether an index exists or not.
    /// </summary>
    /// <param name="indexName">The index to search for.</param>
    /// <returns>True when the index exists.</returns>
    public bool IndexExists(string indexName)
    {
        string sql = $"""
            SELECT count(*) FROM sqlite_master
            WHERE type='index' and name='{Table}_{indexName}';
            """;

        using var stmt = new SQLiteStmt(db, sql);
        stmt.BindText(1, indexName);
        stmt.Step();

        var value = stmt.ColumnInt(0);
        return value == 1;
    }

    /// <summary>
    /// Create an index on a json parameter using the <b> ->> </b> opperator.
    /// Parameter can include nested json values eg: assets.house.location
    /// </summary>
    /// <param name="indexName">The name of the index. If this name exists the index won't be created.</param>
    /// <param name="parameter">The json parameter to create the index for.</param>
    /// <remarks>Index on sqlite is always <see cref="Table"/>_<paramref name="indexName"/></remarks>
    public void CreateIndex(string indexName, string parameter)
    {
        string sql = $"""
            CREATE INDEX IF NOT EXISTS '{Table}_{indexName}'
            ON {Table}(json ->> '$.{parameter}');
            """;

        using var stmt = new SQLiteStmt(db, sql);
        stmt.Step();
    }

    /// <summary>
    /// Combination of <see cref="DeleteIndex(string)"/> and <see cref="CreateIndex(string, string)"/>
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="parameter">The json parameter to update the index for.</param>
    /// <remarks>Index on sqlite is always <see cref="Table"/>_<paramref name="indexName"/></remarks>
    public void UpdateIndex(string indexName, string parameter)
    {
        DeleteIndex(indexName);
        CreateIndex(indexName, parameter);
    }

    /// <summary>
    /// Delete an index if it exists.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <remarks>Index on sqlite is always <see cref="Table"/>_<paramref name="indexName"/></remarks>
    public void DeleteIndex(string indexName)
    {
        string sql = $"""
            DROP INDEX IF EXISTS '{Table}_{indexName}'
            """;

        using var stmt = new SQLiteStmt(db, sql);
        stmt.Step();
    }

    #endregion

    #region Query

    // todo: Implement query capabilities.

    #endregion

    #region View

    // todo: Implement view usage.

    #endregion

    /// <inheritdoc/>
    /// <remarks>
    /// This will close and dispose the underlying database connection.
    /// </remarks>
    public void Dispose()
    {
        if (!open) return;

        open = false;
        foreach (var d in disposables) d.Dispose();
        sqlite3_close_v2(db);
        db.Dispose();
    }
}
