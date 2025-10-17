using System.Collections;
using System.Text.Json;

namespace NoSQLite;

internal sealed class SQLiteStmt : IDisposable
{
    private readonly sqlite3 db;
    private readonly sqlite3_stmt stmt;

#if NET9_0_OR_GREATER
    private readonly Lock locker = new();
#else
    private readonly object locker = new();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteStmt"/> class using a SQL string.
    /// </summary>
    /// <param name="db">The SQLite database connection to use for this statement.</param>
    /// <param name="sql">The SQL statement to prepare and execute as a string.</param>
    /// <param name="disposables">An optional list to which this statement will be added for disposal management.</param>
    public SQLiteStmt(sqlite3 db, string sql, List<IDisposable>? disposables = null)
    {
        this.db = db;
        sqlite3_prepare_v2(db, sql, out stmt);
        disposables?.Add(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteStmt"/> class using a SQL byte span.
    /// </summary>
    /// <param name="db">The SQLite database connection to use for this statement.</param>
    /// <param name="sql">The SQL statement to prepare and execute as a <see cref="ReadOnlySpan{byte}"/>.</param>
    /// <param name="disposables">An optional list to which this statement will be added for disposal management.</param>
    public SQLiteStmt(sqlite3 db, ReadOnlySpan<byte> sql, List<IDisposable>? disposables = null)
    {
        this.db = db;
        sqlite3_prepare_v2(db, sql, out stmt);
        disposables?.Add(this);
    }

    /// <summary>
    /// Executes the SQL statement by stepping through it.
    /// </summary>
    /// <param name="bind">An optional delegate that binds parameters to the statement before execution.</param>
    /// <param name="shouldThrow">Indicates whether exceptions should be thrown on errors.</param>
    /// <remarks>The statement is locked during execution to ensure thread safety. The statement is reset after execution.</remarks>
    public void Execute(SQLiteWriterFunc<SQLiteParameterBinder>? bind, bool shouldThrow = true)
    {
        lock (locker)
        {
            using var step = new SQLiteStep<object?>(db, stmt, bind, static read => null, shouldThrow);
        }
    }

    /// <summary>
    /// Executes the SQL statement and returns a result using the provided reader function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the reader function.</typeparam>
    /// <param name="bind">An optional delegate that binds parameters to the statement before execution.</param>
    /// <param name="read">A delegate that processes the <see cref="SQLiteResultReader"/> and returns a result of type <typeparamref name="TResult"/>.</param>
    /// <param name="shouldThrow">Indicates whether exceptions should be thrown on errors.</param>
    /// <returns>The result produced by the <paramref name="read"/> delegate after executing the statement.</returns>
    /// <remarks>The statement is locked during execution to ensure thread safety. The statement is reset after execution.</remarks>
    public TResult Execute<TResult>(SQLiteWriterFunc<SQLiteParameterBinder>? bind, SQLiteReaderFunc<SQLiteResultReader, TResult> read, bool shouldThrow = true)
    {
        lock (locker)
        {
            using var step = new SQLiteStep<TResult>(db, stmt, bind, read, shouldThrow);
            return step.Result;
        }
    }

    /// <summary>
    /// Executes the SQL statement and returns an array of results using the provided reader function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the reader function.</typeparam>
    /// <param name="bind">An optional delegate that binds parameters to the statement before execution.</param>
    /// <param name="read">A delegate that processes the <see cref="SQLiteResultReader"/> and returns a result of type <typeparamref name="TResult"/>.</param>
    /// <param name="shouldThrow">Indicates whether exceptions should be thrown on errors.</param>
    /// <returns>
    /// An array of results produced by the <paramref name="read"/> delegate for each row returned by the statement.
    /// </returns>
    /// <remarks>
    /// The statement is locked during execution to ensure thread safety. The statement is reset after execution.
    /// </remarks>
    public TResult[] ExecuteMany<TResult>(SQLiteWriterFunc<SQLiteParameterBinder>? bind, SQLiteReaderFunc<SQLiteResultReader, TResult> read, bool shouldThrow = true)
    {
        lock (locker)
        {
            using var steps = new SQLiteSteps<TResult>(db, stmt, bind, read, shouldThrow);
            var results = new List<TResult>();
            while (steps.MoveNext())
            {
                results.Add(steps.Current);
            }
            return [.. results];
        }
    }

    /// <summary>
    /// Finalize this statement.
    /// </summary>
    public void Dispose()
    {
        sqlite3_finalize(stmt);
    }

    private readonly ref struct SQLiteStep<TResult> : IDisposable
    {
        private readonly sqlite3_stmt stmt;

        public TResult Result { get; }

        public SQLiteStep(sqlite3 db, sqlite3_stmt stmt, SQLiteWriterFunc<SQLiteParameterBinder>? bind, SQLiteReaderFunc<SQLiteResultReader, TResult> read, bool shouldThrow)
        {
            this.stmt = stmt;
            bind?.Invoke(new(stmt));
            var result = shouldThrow ? sqlite3_step(stmt).CheckResult(db, $"") : sqlite3_step(stmt);
            Result = read(new(stmt, result));
            if (bind is { })
            {
                sqlite3_clear_bindings(stmt);
            }
        }

        public void Dispose()
        {
            sqlite3_reset(stmt);
        }
    }

    private ref struct SQLiteSteps<TResult> : IEnumerator<TResult>, IDisposable
    {
        private readonly sqlite3 db;
        private readonly sqlite3_stmt stmt;
        private readonly SQLiteReaderFunc<SQLiteResultReader, TResult> read;
        private readonly bool shouldThrow;

        public TResult Current { get; private set; } = default!;

        public SQLiteSteps(sqlite3 db, sqlite3_stmt stmt, SQLiteWriterFunc<SQLiteParameterBinder>? bind, SQLiteReaderFunc<SQLiteResultReader, TResult> read, bool shouldThrow)
        {
            this.db = db;
            this.stmt = stmt;
            this.read = read;
            this.shouldThrow = shouldThrow;
            bind?.Invoke(new(stmt));
        }

        public bool MoveNext()
        {
            var result = shouldThrow ? sqlite3_step(stmt).CheckResult(db, $"") : sqlite3_step(stmt);
            if (result is not SQLITE_ROW)
            {
                return false;
            }
            Current = read(new(stmt, result));
            return true;
        }

        public readonly void Reset()
        {
            throw new NotSupportedException();
        }

        public readonly void Dispose()
        {
            sqlite3_reset(stmt);
            sqlite3_clear_bindings(stmt);
        }

        readonly object IEnumerator.Current => Current!;
    }
}

#if NET9_0_OR_GREATER
internal readonly ref struct SQLiteParameterBinder
#else
internal readonly struct SQLiteParameterBinder
#endif
{
    private readonly sqlite3_stmt stmt;

    public SQLiteParameterBinder(sqlite3_stmt stmt)
    {
        this.stmt = stmt;
    }

    /// <summary>
    /// Binds a blob value to the specified parameter index in the statement.
    /// </summary>
    /// <remarks>Index starts from 1.</remarks>
    /// <param name="index">The parameter index to bind to, starting from 1.</param>
    /// <param name="value">The blob value to bind as a <see cref="ReadOnlySpan{byte}"/>.</param>
    public void Blob(int index, ReadOnlySpan<byte> value) => sqlite3_bind_blob(stmt, index, value);

    /// <summary>
    /// Bind text to a parameter.
    /// </summary>
    /// <remarks>Index starts from 1.</remarks>
    /// <param name="index">The parameter index to bind to, starting from 1.</param>
    /// <param name="value">The value to bind.</param>
    public void Text(int index, string value) => sqlite3_bind_text(stmt, index, value);

    /// <summary>
    /// Bind text to a parameter.
    /// </summary>
    /// <remarks>Index starts from 1.</remarks>
    /// <param name="index">The parameter index to bind to, starting from 1.</param>
    /// <param name="value">The value to bind.</param>
    public void Text(int index, ReadOnlySpan<byte> value) => sqlite3_bind_text(stmt, index, value);

    /// <summary>
    /// Bind an integer value to a parameter.
    /// </summary>
    /// <remarks>Index starts from 1.</remarks>
    /// <param name="index">The parameter index to bind to, starting from 1.</param>
    /// <param name="value">The integer value to bind.</param>
    public void Int(int index, int value) => sqlite3_bind_int(stmt, index, value);

    /// <summary>
    /// Clear all bound parameters for the current statement.
    /// </summary>
    /// <remarks>
    /// This method clears all parameter bindings, allowing the statement to be reused with new values.
    /// </remarks>
    public void Clear() => sqlite3_clear_bindings(stmt);
}

#if NET9_0_OR_GREATER
internal delegate void SQLiteWriterFunc<TWriter>(TWriter writer) where TWriter : allows ref struct;
#else
internal delegate void SQLiteWriterFunc<TWriter>(TWriter writer);
#endif

#if NET9_0_OR_GREATER
internal readonly ref struct SQLiteResultReader
#else
internal readonly struct SQLiteResultReader
#endif
{
    private readonly sqlite3_stmt stmt;

    public int Result { get; }

    public SQLiteResultReader(sqlite3_stmt stmt, int result)
    {
        this.stmt = stmt;
        Result = result;
    }

    public int Count() => sqlite3_column_count(stmt);

    /// <summary>
    /// Get the value of a number column as <see cref="int"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as <see cref="int"/>.</returns>
    public int Int(int index) => sqlite3_column_int(stmt, index);

    /// <summary>
    /// Get the value of a number column as <see cref="int"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as <see cref="int"/>.</returns>
    public long Long(int index) => sqlite3_column_int64(stmt, index);

    /// <summary>
    /// Get the value of a text column as <see cref="string"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as <see cref="string"/>.</returns>
    public string Text(int index) => sqlite3_column_text(stmt, index).utf8_to_string();

    /// <summary>
    /// Get the value of a text/blob column as a series of <see cref="byte"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <returns>The column value as a series of <see cref="byte"/>.</returns>
    public ReadOnlySpan<byte> Blob(int index) => sqlite3_column_blob(stmt, index);

    /// <summary>
    /// Get the value of a text/blob column and deserialize it to the specified type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the column value to.</typeparam>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <param name="jsonOptions">The serializer options to use, or <c>null</c> for default options.</param>
    /// <returns>
    /// The column value deserialized as <typeparamref name="T"/>, or <c>null</c> if the value is <c>null</c>.
    /// </returns>
    /// <exception cref="JsonException">Thrown if the JSON is invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown if the type is not supported.</exception>
    public T? Deserialize<T>(int index, JsonSerializerOptions? jsonOptions = null)
    {
        var bytes = Blob(index);
        var value = JsonSerializer.Deserialize<T>(bytes, jsonOptions);
        return value;
    }

    /// <summary>
    /// Get the value of a text/blob column as a <see cref="JsonDocument"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <param name="jsonOptions">The serializer options to use, or <c>null</c> for default options.</param>
    /// <returns>
    /// The column value deserialized as a <see cref="JsonDocument"/>, or <c>null</c> if the value is <c>null</c>.
    /// </returns>
    /// <exception cref="JsonException">Thrown if the JSON is invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown if the type is not supported.</exception>
    public JsonDocument? DeserializeDocument(int index, JsonSerializerOptions? jsonOptions = null)
    {
        var bytes = Blob(index);
        var value = JsonSerializer.Deserialize<JsonDocument>(bytes, jsonOptions);
        return value;
    }

    /// <summary>
    /// Get the value of a text/blob column as a <see cref="JsonElement"/>.
    /// </summary>
    /// <remarks>Index starts from 0.</remarks>
    /// <param name="index">The column to read the value from, starting from 0.</param>
    /// <param name="jsonOptions">The serializer options to use, or <c>null</c> for default options.</param>
    /// <returns>
    /// The column value deserialized as a <see cref="JsonElement"/>, or <c>null</c> if the value is <c>null</c>.
    /// </returns>
    /// <exception cref="JsonException">Thrown if the JSON is invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown if the type is not supported.</exception>
    public JsonElement? DeserializeElement(int index, JsonSerializerOptions? jsonOptions = null)
    {
        var bytes = Blob(index);
        var value = JsonSerializer.Deserialize<JsonElement>(bytes, jsonOptions);
        return value;
    }
}

#if NET9_0_OR_GREATER
internal delegate TResult SQLiteReaderFunc<TReader, TResult>(TReader reader) where TReader : allows ref struct;
#else
internal delegate TResult SQLiteReaderFunc<TReader, TResult>(TReader reader);
#endif
