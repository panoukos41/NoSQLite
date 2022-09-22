namespace NoSQLite;

/// <summary></summary>
public sealed class NoSQLiteException : Exception
{
    internal NoSQLiteException(string message) : base(message)
    {
    }

    internal NoSQLiteException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
