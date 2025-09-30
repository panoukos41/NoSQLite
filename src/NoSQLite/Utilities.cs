using System.Runtime.CompilerServices;

namespace NoSQLite;

using static SQLitePCL.raw;

internal static class Throw
{
    public static void KeyNotFound(bool condition, [InterpolatedStringHandlerArgument("condition")] ref ConditionInterpolation message)
    {
        if (condition)
        {
            throw new KeyNotFoundException(message.ToString());
        }
    }
}

internal static class Extensions
{
    public static void CheckResult(this sqlite3 db, int result, [InterpolatedStringHandlerArgument("result")] ref SQLiteCodeInterpolation message)
    {
        if (message.ShouldThrow)
        {
            throw new NoSQLiteException($"{message.ToString()}. SQLite info, code: {result}, message: {sqlite3_errmsg(db).utf8_to_string()}");
        }
    }
}

[InterpolatedStringHandler]
internal readonly ref struct SQLiteCodeInterpolation
{
    private readonly DefaultInterpolatedStringHandler _innerHandler;

    /// <summary>
    /// Indicates that the result code is invalid and an exception
    /// should be thrown with the message that has been created.
    /// </summary>
    public bool ShouldThrow { get; }

    public SQLiteCodeInterpolation(
        int literalLength,
        int formattedCount,
        int result,
        out bool shouldAppend)
    {
        // Here we determine which sqlite codes are OK.
        // This is used in conjunction with the "NoSQLiteException".
        if (result is SQLITE_DONE or SQLITE_OK or SQLITE_ROW)
        {
            shouldAppend = false;
            ShouldThrow = false;
            return;
        }

        _innerHandler = new(literalLength, formattedCount);
        shouldAppend = true;
        ShouldThrow = true;
    }

    public void AppendLiteral(string message) => _innerHandler.AppendLiteral(message);

    public void AppendFormatted<T>(T message) => _innerHandler.AppendFormatted(message);

    public override string ToString() => _innerHandler.ToString();

    public string ToStringAndClear() => _innerHandler.ToStringAndClear();
}

[InterpolatedStringHandler]
internal readonly ref struct ConditionInterpolation
{
    private readonly DefaultInterpolatedStringHandler _innerHandler;

    public ConditionInterpolation(
        int literalLength,
        int formattedCount,
        bool condition,
        out bool shouldAppend)
    {
        if (condition)
        {
            _innerHandler = new(literalLength, formattedCount);
            shouldAppend = true;
            return;
        }
        shouldAppend = false;
    }

    public void AppendLiteral(string message) => _innerHandler.AppendLiteral(message);

    public void AppendFormatted<T>(T message) => _innerHandler.AppendFormatted(message);

    public override string ToString() => _innerHandler.ToString();

    public string ToStringAndClear() => _innerHandler.ToStringAndClear();
}
