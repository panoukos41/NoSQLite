using SQLitePCL;

namespace NoSQLite;

/// <summary>
/// Wrapper class that manages the indexes of a <see cref="NoSQLiteConnection"/>.
/// </summary>
public sealed class NoSQLiteIndexes
{
    private readonly sqlite3 db;

    internal NoSQLiteIndexes(sqlite3 db) => this.db = db;

    /// <summary>
    /// Check whether an index exists or not.
    /// </summary>
    /// <param name="indexName">The index to search for.</param>
    /// <returns>True when the index exists.</returns>
    public bool Exists(string indexName)
    {
        const string sql = """
            SELECT count(*) FROM sqlite_master
            WHERE type='index' and name=?;
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.BindText(1, indexName);
        stmt.Step();

        var value = stmt.ColumnInt(0);
        return value == 1;
    }

    // todo: Improve creation.
    // todo: Provide Get method.

    /// <summary>
    /// Create an index on a json parameter using the <i><b>json_extract</b></i> method.
    /// Parameter can include nested json values eg: assets.house.location
    /// </summary>
    /// <param name="indexName">The name of the index. If this name exists the index won't be created.</param>
    /// <param name="parameter">The json parameter to create the index for.</param>
    public void Create(string indexName, string parameter)
    {
        string sql = $"""
            CREATE INDEX IF NOT EXISTS {indexName}
            ON documents(json_extract(json, '$.{parameter}'));
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.Step();
    }

    /// <summary>
    /// Combination of <see cref="Delete(string)"/> and <see cref="Create(string, string)"/>
    /// </summary>
    /// <param name="indexName">The name of the index. If this name exists the index won't be created.</param>
    /// <param name="parameter">The json parameter to create the index for.</param>
    public void Update(string indexName, string parameter)
    {
        Delete(indexName);
        Create(indexName, parameter);
    }

    public void Delete(string indexName)
    {
        string sql = $"""
            DROP INDEX IF EXISTS {indexName}
            """;

        using var stmt = new SqliteStatement(db, sql);
        stmt.Step();
    }
}
