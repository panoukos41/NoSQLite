using System.Linq.Expressions;
using System.Text.Json;

namespace NoSQLite;

[Preserve(AllMembers = true)]
public sealed class NoSQLiteTable : IDisposable
{
    private readonly List<IDisposable> disposables = new(6);
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

    private SQLiteStmt CountStmt => field ??= new(db, disposables: disposables, sql: $"""
        SELECT count(*) FROM "{Table}"
        """);

    public int Count()
    {
        return CountStmt.Execute(null, static r => r.Int(0));
    }

    private SQLiteStmt LongCountStmt => field ??= new(db, disposables: disposables, sql: $"""
        SELECT count(*) FROM "{Table}"
        """);

    public long LongCount()
    {
        var table = Table;
        return LongCountStmt.Execute(null, static r => r.Long(0));
    }

    private SQLiteStmt AllStmt => field ??= new(db, disposables: disposables, sql: $"""
        SELECT "documents" FROM "{Table}"
        """);

    public T[] All<T>()
    {
        var jsonOptions = JsonOptions;
        return AllStmt.ExecuteMany(null, r => r.Deserialize<T>(0, jsonOptions)!);
    }

    private SQLiteStmt ClearStmt => field ??= new(db, disposables: disposables, sql: $"""
        DELETE FROM "{Table}"
        """);

    public void Clear()
    {
        ClearStmt.Execute(null);
    }

    private SQLiteStmt ExistsStmt => field ??= new(db, disposables: disposables, sql: $"""
        SELECT count(*) FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?;
        """);

    public bool Exists<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        var propertyPath = selector.GetPropertyPath();
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

    private SQLiteStmt FindStmt => field ??= new(db, disposables: disposables, sql: $"""
        SELECT "documents"
        FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?
        """);

    public T Find<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        var propertyPath = selector.GetPropertyPath();
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        return FindStmt.Execute(
            b =>
            {
                b.Text(1, propertyPath);
                b.Text(2, jsonKey);
            },
            r => r.Deserialize<T>(0, JsonOptions)!
        );
    }

    private SQLiteStmt FindPropertyStmt => field ??= new(db, disposables: disposables, sql: $"""
        SELECT "documents"->('$.' || ?)
        FROM "{Table}"
        WHERE "documents"->('$.' || ?) = ?
        """);

    public TProperty FindProperty<T, TKey, TProperty>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TProperty>> propertySelector, TKey key)
    {
        var keyPropertyPath = keySelector.GetPropertyPath();
        var propertyPath = propertySelector.GetPropertyPath();
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        return FindPropertyStmt.Execute(
            b =>
            {
                b.Text(1, propertyPath);
                b.Text(2, keyPropertyPath);
                b.Text(3, jsonKey);
            },
            r => r.Deserialize<TProperty>(0, JsonOptions)!
        );
    }

    private SQLiteStmt InsertStmt => field ??= new(db, disposables: disposables, sql: $"""
        INSERT INTO "{Table}"("documents") VALUES (?)
        """);

    public void Insert<T>(T obj)
    {
        var document = JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions);

        InsertStmt.Execute(b => b.Blob(1, document));
    }

    public void Update<T, TKey>(T document, Expression<Func<T, TKey>> selector)
    {
        using var stmt = new SQLiteStmt(db, $"""
            UPDATE "{Table}"
            SET "documents" = ?
            WHERE "documents"->('$.' || ?) = ?;
            """);

        var propertyPath = selector.GetPropertyPath();
        var key = selector.Compile().Invoke(document);
        var jsonDocument = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        stmt.Execute(b =>
        {
            b.Blob(1, jsonDocument);
            b.Text(2, propertyPath);
            b.Text(3, jsonKey);
        });
    }

    public void Remove<T, TKey>(Expression<Func<T, TKey>> selector, TKey key)
    {
        using var stmt = new SQLiteStmt(db, $"""
            DELETE FROM "{Table}"
            WHERE "documents"->('$.' || ?) = ?;
            """);

        var propertyPath = selector.GetPropertyPath();
        var jsonKey = JsonSerializer.SerializeToUtf8Bytes(key, JsonOptions);

        stmt.Execute(b =>
        {
            b.Text(1, propertyPath);
            b.Text(2, jsonKey);
        });
    }

    public bool IndexExists(string indexName)
    {
        using var stmt = new SQLiteStmt(db, """
            SELECT name FROM "sqlite_master"
            WHERE type='index' AND name = ?;
            """u8);

        var index = $"{Table}_{indexName}";

        return stmt.Execute(b => b.Text(1, index), static r => r.Result is SQLITE_ROW, shouldThrow: false);
    }

    public void CreateIndex<T, TKey>(Expression<Func<T, TKey>> selector, string indexName)
    {
        var propertyPath = selector.GetPropertyPath();

        // can't use parameter (?) here so we have to create a new statement every time.
        using var stmt = new SQLiteStmt(db, $"""
            CREATE INDEX IF NOT EXISTS "{Table}_{indexName}"
            ON "{Table}" ("documents"->'$.{propertyPath}')
            """);

        stmt.Execute(null);
    }

    public bool DeleteIndex(string indexName)
    {
        using var stmt = new SQLiteStmt(db, $"""
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
