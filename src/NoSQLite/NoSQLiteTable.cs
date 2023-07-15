using SQLitePCL;
using System.Text.Json;

namespace NoSQLite;

using static SQLitePCL.raw;

/// <summary>
/// todo: Summary
/// </summary>
[Preserve(AllMembers = true)]
public sealed class NoSQLiteTable : IDisposable
{
    private readonly List<IDisposable> disposables = new(6);
    private readonly sqlite3 db;

    internal NoSQLiteTable(string table, NoSQLiteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection, nameof(connection));
        ArgumentNullException.ThrowIfNull(table, nameof(table));

        Table = table;
        Connection = connection;
        JsonOptions = connection.JsonOptions;
        db = connection.db;

        connection.CreateTable(table);
        connection.tables.Add(table, this);

        #region Lazy statment initialization

        countStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                SELECT count(*) FROM "{Table}";
                """);

            disposables.Add(stmt);
            return stmt;
        });

        existsStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                SELECT count(*) FROM "{Table}"
                WHERE id is ?;
                """);

            disposables.Add(stmt);
            return stmt;
        });

        allStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                SELECT json FROM "{Table}";
                """);
            disposables.Add(stmt);
            return stmt;
        });

        findStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                SELECT json
                FROM "{Table}"
                WHERE id is ?;
                """);

            disposables.Add(stmt);
            return stmt;
        });

        insertStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                REPLACE INTO "{Table}"
                VALUES (?, json(?));
                """);

            disposables.Add(stmt);
            return stmt;
        });

        removeStmt = new(() =>
        {
            var stmt = new SQLiteStmt(db, $"""
                DELETE FROM "{Table}"
                WHERE id is ?;
                """);

            disposables.Add(stmt);
            return stmt;
        });
        #endregion
    }

    /// <summary>
    /// The connection that created and manages this table.
    /// </summary>
    /// <remarks>If the connection is disposed this will also be disposed.</remarks>
    public NoSQLiteConnection Connection { get; }

    /// <summary>
    /// The name of the table this connection will use.
    /// </summary>
    public string Table { get; }

    /// <summary>
    /// Gets or Sets the JSON serializer options used to serialzie/deserialize the documents.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>
    /// Deletes all rows from a table.
    /// </summary>
    public void Clear() => Connection.Clear(Table);

    /// <summary>
    /// Drops the table and then Creates it again. This will delete all indexes views etc.
    /// </summary>
    /// <remarks>See <see href="https://sqlite.org/lang_droptable.html"/> for more info.</remarks>
    public void DropAndCreate() => Connection.DropAndCreate(Table);

    #region Basic

    private readonly Lazy<SQLiteStmt> countStmt;

    public int Count()
    {
        var stmt = countStmt.Value;
        lock (countStmt)
        {
            stmt.Step();

            var count = stmt.ColumnInt(0);
            stmt.Reset();
            return count;
        }
    }

    public long LongCount()
    {
        var stmt = countStmt.Value;
        lock (countStmt)
        {
            stmt.Step();

            var count = stmt.ColumnLong(0);
            stmt.Reset();
            return count;
        }
    }

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
            return value != 0;
        }
    }

    private readonly Lazy<SQLiteStmt> allStmt;

    public IEnumerable<T> All<T>()
    {
        var stmt = allStmt.Value;
        lock (allStmt)
        {
            start:
            var result = stmt.Step();

            if (result is not SQLITE_ROW)
            {
                stmt.Reset();
                yield break;
            }

            var value = stmt.ColumnDeserialize<T>(0, JsonOptions);
            yield return value!;
            goto start;
        }
    }

    public IEnumerable<byte[]> AllBytes()
    {
        var stmt = allStmt.Value;
        lock (allStmt)
        {
            start:
            var result = stmt.Step();

            if (result is not SQLITE_ROW)
            {
                stmt.Reset();
                yield break;
            }

            var bytes = stmt.ColumnBlob(0).ToArray();
            yield return bytes;
            goto start;
        }
    }

    #endregion

    #region Find

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
    public IEnumerable<T> FindMany<T>(IEnumerable<string> ids, bool throwIfNotFound = true)
    {
        foreach (var id in ids)
        {
            var doc = Find<T>(id);

            if (doc is null)
            {
                Throw.KeyNotFound(throwIfNotFound, $"Could not locate the Id '{id}'");
                continue;
            }
            yield return doc;
        }
    }

    /// <summary>
    /// Get Id - <typeparamref name="T"/> pairs.
    /// If an object doesn't exist it will have a null value.
    /// </summary>
    /// <typeparam name="T">The type the objects will deserialize to.</typeparam>
    /// <param name="ids">The ids to search for.</param>
    /// <returns>An enumerable of Id - <typeparamref name="T"/> object pairs.</returns>
    /// <remarks>Duplicate keys are ignored.</remarks>
    public IDictionary<string, T?> FindPairs<T>(IEnumerable<string> ids)
    {
        var dictionary = new Dictionary<string, T?>();
        foreach (var id in ids)
        {
            if (dictionary.ContainsKey(id)) continue;

            dictionary.Add(id, Find<T>(id));
        }
        return dictionary;
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
    public IEnumerable<byte[]> FindBytesMany(IEnumerable<string> ids, bool throwIfNotFound = true)
    {
        foreach (var id in ids)
        {
            var bytes = FindBytes(id);

            if (bytes is null)
            {
                Throw.KeyNotFound(throwIfNotFound, $"Could not locate the Id '{id}'");
                continue;
            }
            yield return bytes;
        }
    }

    /// <summary>
    /// Get Id - byte array pairs.
    /// If an object doesn't exist it will have a null value
    /// for the corresponding Id in the dictionary.
    /// </summary>
    /// <param name="ids">The ids to search for.</param>
    /// <returns>A dictionary of Id - <typeparamref name="T"/> object pairs.</returns>
    /// <remarks>Duplicate keys are just put on the key again.</remarks>
    public IDictionary<string, byte[]?> FindBytesPairs(IEnumerable<string> ids)
    {
        var pairs = new Dictionary<string, byte[]?>();
        foreach (var id in ids)
        {
            var bytes = FindBytes(id);
            pairs[id] = bytes;
        }
        return pairs;
    }

    #endregion

    #region Insert

    private readonly Lazy<SQLiteStmt> insertStmt;

    public void Insert<T>(string id, T obj)
    {
        InsertBytes(id, JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions));
    }

    public void InsertMany<T>(IDictionary<string, T> keyValuePairs)
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

    public void InsertBytesMany(IDictionary<string, byte[]> keyValuePairs)
    {
        using var transaction = new SQLiteTransaction(db);
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
    public void RemoveMany(IEnumerable<string> ids)
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
            WHERE type='index' and name="{Table}_{indexName}";
            """;

        using var stmt = new SQLiteStmt(db, sql);
        stmt.Step();

        var value = stmt.ColumnInt(0);
        return value != 0;
    }

    /// <summary>
    /// Create an index on a json parameter using the <b> json_extract(json, '$.param') </b> opperator.
    /// Parameter can include nested json values eg: assets.house.location
    /// </summary>
    /// <param name="indexName">The name of the index. If this name exists the index won't be created.</param>
    /// <param name="parameter">The json parameter to create the index for.</param>
    /// <remarks>
    /// Parameter names are case sensitive.<br/>
    /// Index name on sqlite will always be created as <see cref="Table"/>_<paramref name="indexName"/>
    /// </remarks>
    public bool CreateIndex(string indexName, string parameter)
    {
        string sql = $"""
            CREATE INDEX "{Table}_{indexName}"
            ON "{Table}"(json_extract("json", '$.{parameter}'));
            """;

        return sqlite3_exec(db, sql) == SQLITE_OK;
    }

    /// <summary>
    /// Combination of <see cref="DeleteIndex(string)"/> and <see cref="CreateIndex(string, string)"/>
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="parameter">The json parameter to update the index for.</param>
    /// <remarks>Index on sqlite is always <see cref="Table"/>_<paramref name="indexName"/></remarks>
    public void RecreateIndex(string indexName, string parameter)
    {
        DeleteIndex(indexName);
        CreateIndex(indexName, parameter);
    }

    /// <summary>
    /// Delete an index if it exists.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <remarks>Index on sqlite is always <see cref="Table"/>_<paramref name="indexName"/></remarks>
    public bool DeleteIndex(string indexName)
    {
        string sql = $"""
            DROP INDEX "{Table}_{indexName}"
            """;

        return sqlite3_exec(db, sql) == SQLITE_OK;
    }

    #endregion

    #region Query

    // todo: Implement query capabilities.

    #endregion

    #region View

    // todo: Implement view usage.

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        Connection.tables.Remove(Table);
        if (disposables.Count <= 0) return;

        foreach (var d in disposables) d.Dispose();
        disposables.Clear();
    }
}
