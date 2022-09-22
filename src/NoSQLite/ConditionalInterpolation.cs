using System.Runtime.CompilerServices;

namespace NoSQLite;

using static SQLitePCL.raw;

[InterpolatedStringHandler]
internal ref struct ConditionalInterpolation
{
    private readonly DefaultInterpolatedStringHandler _innerHandler;

    /// <summary>
    /// Inidicates that the code is invalid and an exception
    /// should be throwen with the message that has been created.
    /// </summary>
    public bool ShouldThrow { get; }

    public ConditionalInterpolation(
        int literalLength,
        int formattedCount,
        int code,
        out bool shouldAppend)
    {
        // Here we determine which sqlite codes are OK.
        // This is used in conjuction with the <see cref="NoSQLiteException"/>.
        if (code == SQLITE_DONE ||
            code == SQLITE_OK)
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
