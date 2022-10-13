using SQLitePCL;
using System.Text.Json;

namespace NoSQLite;

using static SQLitePCL.raw;

internal sealed class SQLiteStmt : IDisposable
{
    private readonly sqlite3 db;
    private readonly sqlite3_stmt stmt;

    /// <summary>
    /// Initialize a new <see cref="SqliteStatement"/>.
    /// </summary>
    /// <param name="db">The database that runs the statements.</param>
    /// <param name="sql">The sql to execute.</param>
    public SQLiteStmt(sqlite3 db, string sql)
    {
        this.db = db;
        sqlite3_prepare_v2(db, sql, out stmt);
    }

    #region Bind

    /// <summary>
    /// Bind text to a paramter.
    /// </summary>
    /// <remarks>Index starts from 1.</remarks>
    /// <param name="index">The parameter index to bind to, starting from 1.</param>
    /// <param name="value">The value to bind.</param>
    public void BindText(int index, string value) => sqlite3_bind_text(stmt, index, value);

    /// <summary>
    /// Bind text to a paramter.
    /// </summary>
    /// <remarks>Index starts from 1.</remarks>
    /// <param name="index">The parameter index to bind to, starting from 1.</param>
    /// <param name="value">The value to bind.</param>
    public void BindText(int index, ReadOnlySpan<byte> value) => sqlite3_bind_text(stmt, index, value);

    #endregion

    #region Column

    /// <summary>
    /// Get the value of a number column as <see cref="int"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as <see cref="int"/>.</returns>
    public int ColumnInt(int index) => sqlite3_column_int(stmt, index);

    /// <summary>
    /// Get the value of a text column as <see cref="string"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as <see cref="string"/>.</returns>
    public string ColumnText(int index) => sqlite3_column_text(stmt, index).utf8_to_string();

    /// <summary>
    /// Get the value of a text/blobl column as a series of <see cref="byte"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as a series of <see cref="byte"/>.</returns>
    public ReadOnlySpan<byte> ColumnBlob(int index) => sqlite3_column_blob(stmt, index);

    /// <summary>
    /// Get the value of a text column deserialized to <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <typeparam name="T">The type to deserialize from.</typeparam>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <param name="jsonOptions">The serializer options to use.</param>
    /// <returns>An instance of <typeparamref name="T"/> or null.</returns>
    /// <exception cref="JsonException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public T? ColumnDeserialize<T>(int index, JsonSerializerOptions? jsonOptions = null)
    {
        var bytes = ColumnBlob(index);
        var value = JsonSerializer.Deserialize<T>(bytes, jsonOptions);
        return value;
    }

    #endregion

    /// <summary>
    /// Execute the SQL statement. Depending on the SQL this
    /// can be executed multiple times returning different results
    /// for different sql statements.
    /// </summary>
    public int Step() => sqlite3_step(stmt);

    /// <summary>
    /// Reset this <see cref="SqliteStatement"/> back to the start
    /// as if it was just created.
    /// </summary>
    /// <remarks>This will lose all progress before reset was called.</remarks>
    public int Reset() => sqlite3_reset(stmt);

    /// <summary>
    /// Get the error message the database has currently emmited.
    /// </summary>
    /// <returns>The error message from the database.</returns>
    public string Error() => sqlite3_errmsg(db).utf8_to_string();

    /// <summary>
    /// Finilize this statement.
    /// </summary>
    public void Dispose()
    {
        sqlite3_finalize(stmt);
    }
}
