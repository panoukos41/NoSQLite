using SQLitePCL;

namespace NoSQLite;

using static SQLitePCL.raw;

internal ref struct SQLiteTransaction
{
    private readonly sqlite3 db;

    public bool InTransaction { get; private set; }

    /// <summary>
    /// Create a new transaction. If <paramref name="begin"/> is true it begins the transaction immediately.
    /// </summary>
    /// <param name="db">The database to run the transaction on.</param>
    /// <param name="begin">True to begin the transaction immediately</param>
    /// <remarks>Use in a <see langword="using"/> statement to execute <see cref="Commit"/> automatically when disposed.</remarks>
    public SQLiteTransaction(sqlite3 db, bool begin = true)
    {
        this.db = db;
        if (begin) Begin();
    }

    /// <summary>
    /// Begin a transaction.
    /// </summary>
    public int Begin()
    {
        if (InTransaction) return SQLITE_OK;

        InTransaction = true;
        return sqlite3_exec(db, "BEGIN;");
    }

    /// <summary>
    /// Commit a transaction.
    /// </summary>
    public int Commit()
    {
        if (!InTransaction) return SQLITE_OK;

        InTransaction = false;
        return sqlite3_exec(db, "COMMIT");
    }

    /// <summary>
    /// Rollback a transaction.
    /// </summary>
    public int Rollback()
    {
        if (!InTransaction) return SQLITE_OK;

        InTransaction = false;
        return sqlite3_exec(db, "ROLLBACK;");
    }

    /// <summary>
    /// Dispose this transaction. When disposed perfromes <see cref="Commit"/>.
    /// </summary>
    public void Dispose()
    {
        Commit();
    }
}
