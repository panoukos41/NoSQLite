using System.Buffers;

namespace NoSQLite;

/// <summary>
/// Represents a table in a NoSQLite database, providing methods to manage documents and indexes.
/// </summary>
[Preserve(AllMembers = true)]
public sealed class NoSQLiteTable : IDisposable
{
    private readonly List<IDisposable> disposables = [];
    private readonly sqlite3 db;

    internal NoSQLiteTable(string table, NoSQLiteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(table);

        Table = table;
        Connection = connection;
        db = connection.db;

        connection.CreateTable(table);
    }

    /// <summary>
    /// Gets the <see cref="NoSQLiteConnection"/> associated with this table.
    /// </summary>
    public NoSQLiteConnection Connection { get; }

    /// <summary>
    /// Gets the name of the table.
    /// </summary>
    public string Table { get; }

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used for JSON serialization and deserialization.
    /// </summary>
    public JsonSerializerOptions? JsonOptions => Connection.JsonOptions;

    internal SQLiteStmt NewStmt(string sql) => new(db, JsonOptions, sql, disposables);

    internal SQLiteStmt NewStmt(ReadOnlySpan<byte> sql) => new(db, JsonOptions, sql, disposables);

    private SQLiteStmt CountStmt => field ??= NewStmt($"""
        SELECT count(*) FROM "{Table}"
        """);

    /// <summary>
    /// Gets the number of documents in the table.
    /// </summary>
    /// <returns>The count of documents as an <see cref="int"/>.</returns>
    public int Count()
    {
        return CountStmt.Execute(null, static r => r.Int(0));
    }

    private SQLiteStmt LongCountStmt => field ??= NewStmt($"""
        SELECT count(*) FROM "{Table}"
        """);

    /// <summary>
    /// Gets the number of documents in the table as a <see cref="long"/>.
    /// </summary>
    /// <returns>The count of documents as a <see cref="long"/>.</returns>
    public long LongCount()
    {
        return LongCountStmt.Execute(null, static r => r.Long(0));
    }

    private SQLiteStmt AllStmt => field ??= NewStmt($"""
        SELECT "documents" FROM "{Table}"
        """);

    /// <summary>
    /// Gets all documents in the table as an array of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize each document to.</typeparam>
    /// <returns>An array of all documents in the table.</returns>
    public T[] All<T>()
    {
        return AllStmt.ExecuteMany(null, static r => r.Deserialize<T>(0)!);
    }

    private SQLiteStmt ClearStmt => field ??= NewStmt($"""
        DELETE FROM "{Table}"
        """);

    /// <summary>
    /// Removes all documents from the table.
    /// </summary>
    public void Clear()
    {
        ClearStmt.Execute(null);
    }

    private SQLiteStmt ExistsStmt => field ??= NewStmt($"""
        SELECT count(*) FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?;
        """);

    /// <summary>
    /// Determines whether a document with the specified key exists in the table.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="selector">An expression selecting the key property.</param>
    /// <param name="key">The key value to search for.</param>
    /// <returns><see langword="true"/> if a document with the specified key exists; otherwise, <see langword="false"/>.</returns>
    public bool Exists<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        var propertyPath = selector.GetPropertyPath(JsonOptions);
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        return ExistsStmt.Execute(
            b =>
            {
                b.Text(1, propertyPath);
                b.Text(2, jsonKey);
            },
            static b => b.Int(0) is not 0
        );
    }

    private SQLiteStmt FindStmt => field ??= NewStmt($"""
        SELECT "documents"
        FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?
        """);

    /// <summary>
    /// Finds and returns a document by key.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="selector">An expression selecting the key property.</param>
    /// <param name="key">The key value to search for.</param>
    /// <returns>The document matching the specified key.</returns>
    /// <exception cref="NoSQLiteException">Thrown if the key is not found.</exception>
    public T Find<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        var propertyPath = selector.GetPropertyPath(JsonOptions);
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        var found = FindStmt.Execute(
            b =>
            {
                b.Text(1, propertyPath);
                b.Text(2, jsonKey);
            },
            static r => r.Deserialize<T>(0)
        );

        NoSQLiteException.KeyNotFound(found, key);
        return found;
    }

    private SQLiteStmt FindPropertyStmt => field ??= NewStmt($"""
        SELECT "documents"->('$.' || ?)
        FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?
        """);

    /// <summary>
    /// Finds and returns a property value from a document by key.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="keySelector">An expression selecting the key property.</param>
    /// <param name="propertySelector">An expression selecting the property to retrieve.</param>
    /// <param name="key">The key value to search for.</param>
    /// <returns>The property value, or <c>null</c> if not found.</returns>
    public TProperty? FindProperty<T, TKey, TProperty>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TProperty?>> propertySelector, TKey key)
    {
        var keyPropertyPath = keySelector.GetPropertyPath(JsonOptions);
        var propertyPath = propertySelector.GetPropertyPath(JsonOptions);
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        return FindPropertyStmt.Execute(
            b =>
            {
                b.Text(1, propertyPath);
                b.Text(2, keyPropertyPath);
                b.Text(3, jsonKey);
            },
            static r => r.Deserialize<TProperty>(0)
        );
    }

    private SQLiteStmt AddStmt => field ??= NewStmt($"""
        INSERT INTO "{Table}"("documents") VALUES (json(?))
        """);

    /// <summary>
    /// Adds a new document to the table.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="obj">The document to add.</param>
    public void Add<T>(T obj)
    {
        var document = JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions);

        AddStmt.Execute(b => b.Blob(1, document));
    }

    private SQLiteStmt UpdateStmt => field ??= NewStmt($"""
        UPDATE "{Table}"
        SET "documents" = json(?)
        WHERE "documents"->('$.' || ?) = ?;
        """);

    /// <summary>
    /// Updates an existing document in the table.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="document">The updated document.</param>
    /// <param name="selector">An expression selecting the key property.</param>
    public void Update<T, TKey>(T document, Expression<Func<T, TKey>> selector)
    {
        var propertyPath = selector.GetPropertyPath(JsonOptions);
        var key = selector.Compile().Invoke(document);

        UpdateStmt.Execute(b =>
        {
            b.JsonBlob(1, document);
            b.Text(2, propertyPath);
            b.JsonText(3, key);
        });
    }

    private SQLiteStmt DeleteStmt => field ??= NewStmt($"""
        DELETE FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?;
        """);

    /// <summary>
    /// Deletes a document from the table by key.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="selector">An expression selecting the key property.</param>
    /// <param name="key">The key value of the document to remove.</param>
    public void Delete<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        var propertyPath = selector.GetPropertyPath(JsonOptions);

        DeleteStmt.Execute(b =>
        {
            b.Text(1, propertyPath);
            b.JsonText(2, key);
        });
    }

    // https://sqlite.org/json1.html#jins
    private SQLiteStmt InsertStmt => field ??= NewStmt($"""
        UPDATE "{Table}"
        SET "documents" = json_insert("documents", ('$.' || ?), json(?))
        WHERE "documents"->('$.' || ?) = ?
        """);

    /// <summary>
    /// Inserts a property value into a document by key.
    /// </summary>
    /// <remarks>
    /// Overwrite if already exists? <b>NO</b> (including <see langword="null"/> values, only the key matters). <br/>
    /// Create if does not exist? <b>YES</b>
    /// </remarks>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="keySelector">An expression selecting the key property.</param>
    /// <param name="propertySelector">An expression selecting the property to insert.</param>
    /// <param name="key">The key value of the document.</param>
    /// <param name="value">The property value to insert.</param>
    public void Insert<T, TKey, TProperty>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TProperty?>> propertySelector, TKey key, TProperty? value)
    {
        var keyPropertyPath = keySelector.GetPropertyPath(JsonOptions);
        var propertyPath = propertySelector.GetPropertyPath(JsonOptions);

        // cant know if it actaully inserted or not
        InsertStmt.Execute(b =>
        {
            b.Text(1, propertyPath);
            b.JsonBlob(2, value);
            b.Text(3, keyPropertyPath);
            b.JsonText(4, key);
        });
    }

    // https://sqlite.org/json1.html#jins
    private SQLiteStmt ReplaceStmt => field ??= NewStmt($"""
        UPDATE "{Table}"
        SET "documents" = json_replace("documents", ('$.' || ?), json(?))
        WHERE "documents"->('$.' || ?) = ?
        """);

    /// <summary>
    /// Replaces a property value in a document by key.
    /// </summary>
    /// <remarks>
    /// Overwrite if already exists? <b>YES</b> (<see langword="null"/> values won't remove the key). <br/>
    /// Create if does not exist? <b>NO</b>
    /// </remarks>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="keySelector">An expression selecting the key property.</param>
    /// <param name="propertySelector">An expression selecting the property to replace.</param>
    /// <param name="key">The key value of the document.</param>
    /// <param name="value">The property value to replace.</param>
    public void Replace<T, TKey, TProperty>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TProperty?>> propertySelector, TKey key, TProperty? value)
    {
        var keyPropertyPath = keySelector.GetPropertyPath(JsonOptions);
        var propertyPath = propertySelector.GetPropertyPath(JsonOptions);

        // cant know if it actaully replaced or not
        ReplaceStmt.Execute(b =>
        {
            b.Text(1, propertyPath);
            b.JsonBlob(2, value);
            b.Text(3, keyPropertyPath);
            b.JsonText(4, key);
        });
    }

    // https://sqlite.org/json1.html#jins
    private SQLiteStmt SetStmt => field ??= NewStmt($"""
        UPDATE "{Table}"
        SET "documents" = json_set("documents", ('$.' || ?), json(?))
        WHERE "documents"->('$.' || ?) = ?
        """);

    /// <summary>
    /// Sets a property value in a document by key.
    /// </summary>
    /// <remarks>
    /// Overwrite if already exists? <b>YES</b> <br/>
    /// Create if does not exist? <b>YES</b>
    /// </remarks>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="keySelector">An expression selecting the key property.</param>
    /// <param name="propertySelector">An expression selecting the property to set.</param>
    /// <param name="key">The key value of the document.</param>
    /// <param name="value">The property value to set.</param>
    public void Set<T, TKey, TProperty>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TProperty?>> propertySelector, TKey key, TProperty? value)
    {
        var keyPropertyPath = keySelector.GetPropertyPath(JsonOptions);
        var propertyPath = propertySelector.GetPropertyPath(JsonOptions);

        // will set no matter what so failure will throw
        SetStmt.Execute(b =>
        {
            b.Text(1, propertyPath);
            b.JsonBlob(2, value);
            b.Text(3, keyPropertyPath);
            b.JsonText(4, key);
        });
    }

    /// <summary>
    /// Determines whether an index with the specified name exists for this table.
    /// </summary>
    /// <param name="indexName">The name of the index to check.</param>
    /// <returns><see langword="true"/> if the index exists; otherwise, <see langword="false"/>.</returns>
    public bool IndexExists(string indexName)
    {
        using var stmt = new SQLiteStmt(db, JsonOptions, """
            SELECT name FROM "sqlite_master"
            WHERE type='index' AND name = ?;
            """u8);

        var index = $"{Table}_{indexName}";

        return stmt.Execute(b => b.Text(1, index), static r => r.Result is SQLITE_ROW, shouldThrow: false);
    }

    /// <summary>
    /// Creates an index on the specified property of the documents in the table.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="selector">An expression selecting the property to index.</param>
    /// <param name="indexName">The name of the index to create.</param>
    /// <param name="unique">Whether the index should enforce uniqueness.</param>
    public void CreateIndex<T, TKey>(Expression<Func<T, TKey>> selector, string indexName, bool unique = false)
    {
        var propertyPath = selector.GetPropertyPath(JsonOptions);

        // can't use parameter (?) here so we have to create a new statement every time.
        using var stmt = new SQLiteStmt(db, JsonOptions, $"""
            CREATE{(unique ? " UNIQUE" : "")} INDEX IF NOT EXISTS "{Table}_{indexName}"
            ON "{Table}" ("documents"->'$.{propertyPath}')
            """);

        stmt.Execute(null);
    }

    /// <summary>
    /// Deletes an index with the specified name from this table.
    /// </summary>
    /// <param name="indexName">The name of the index to delete.</param>
    /// <returns><see langword="true"/> if the index was deleted; otherwise, <see langword="false"/>.</returns>
    public bool DeleteIndex(string indexName)
    {
        using var stmt = new SQLiteStmt(db, JsonOptions, $"""
            DROP INDEX "{Table}_{indexName}"
            """);

        return stmt.Execute(null, static r => true);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="NoSQLiteTable"/>.
    /// </summary>
    /// <remarks>
    /// Disposes all prepared statements.
    /// </remarks>
    public void Dispose()
    {
        if (disposables.Count <= 0) return;

        var length = disposables.Count;
        var buffer = ArrayPool<NoSQLiteTable>.Shared.Rent(length);
        try
        {
            disposables.CopyTo(buffer, 0);
            disposables.Clear();

            for (int i = 0; i < length; i++) buffer[i].Dispose();
        }
        finally
        {
            ArrayPool<NoSQLiteTable>.Shared.Return(buffer, true);
        }
    }
}
