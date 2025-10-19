namespace NoSQLite;

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

    public NoSQLiteConnection Connection { get; }

    public string Table { get; }

    public JsonSerializerOptions? JsonOptions => Connection.JsonOptions;

    internal SQLiteStmt NewStmt(string sql) => new(db, JsonOptions, sql, disposables);

    internal SQLiteStmt NewStmt(ReadOnlySpan<byte> sql) => new(db, JsonOptions, sql, disposables);

    private SQLiteStmt CountStmt => field ??= NewStmt($"""
        SELECT count(*) FROM "{Table}"
        """);

    public int Count()
    {
        return CountStmt.Execute(null, static r => r.Int(0));
    }

    private SQLiteStmt LongCountStmt => field ??= NewStmt($"""
        SELECT count(*) FROM "{Table}"
        """);

    public long LongCount()
    {
        return LongCountStmt.Execute(null, static r => r.Long(0));
    }

    private SQLiteStmt AllStmt => field ??= NewStmt($"""
        SELECT "documents" FROM "{Table}"
        """);

    public T[] All<T>()
    {
        return AllStmt.ExecuteMany(null, static r => r.Deserialize<T>(0)!);
    }

    private SQLiteStmt ClearStmt => field ??= NewStmt($"""
        DELETE FROM "{Table}"
        """);

    public void Clear()
    {
        ClearStmt.Execute(null);
    }

    private SQLiteStmt ExistsStmt => field ??= NewStmt($"""
        SELECT count(*) FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?;
        """);

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

    private SQLiteStmt RemoveStmt => field ??= NewStmt($"""
        DELETE FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?;
        """);

    public void Remove<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        var propertyPath = selector.GetPropertyPath(JsonOptions);

        RemoveStmt.Execute(b =>
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

    public bool IndexExists(string indexName)
    {
        using var stmt = new SQLiteStmt(db, JsonOptions, """
            SELECT name FROM "sqlite_master"
            WHERE type='index' AND name = ?;
            """u8);

        var index = $"{Table}_{indexName}";

        return stmt.Execute(b => b.Text(1, index), static r => r.Result is SQLITE_ROW, shouldThrow: false);
    }

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

    public bool DeleteIndex(string indexName)
    {
        using var stmt = new SQLiteStmt(db, JsonOptions, $"""
            DROP INDEX "{Table}_{indexName}"
            """);

        return stmt.Execute(null, static r => true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Connection.tables.Remove(Table, out _);
        if (disposables.Count <= 0) return;

        var toDispose = new IDisposable[disposables.Count];
        disposables.CopyTo(toDispose);
        disposables.Clear();

        foreach (var d in toDispose) d.Dispose();
    }
}
