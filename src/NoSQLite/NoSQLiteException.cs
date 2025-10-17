using System.Runtime.CompilerServices;

namespace NoSQLite;

/// <summary>
/// Represents errors that occur during NoSQLite database operations.
/// </summary>
public sealed class NoSQLiteException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoSQLiteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    internal NoSQLiteException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoSQLiteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    internal NoSQLiteException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>
    /// Throws a <see cref="NoSQLiteException"/> with the specified message if the condition is <c>true</c>.
    /// </summary>
    /// <param name="condition">The condition to evaluate. If <c>true</c>, the exception is thrown.</param>
    /// <param name="message">The interpolated message to include in the exception if thrown.</param>
    /// <exception cref="NoSQLiteException">Thrown when <paramref name="condition"/> is <c>true</c>.</exception>
    internal static void If(bool condition, [InterpolatedStringHandlerArgument("condition")] ref ConditionInterpolation message)
    {
        if (condition)
        {
            throw new NoSQLiteException(message.ToString());
        }
    }

    /// <summary>
    /// Throws a <see cref="KeyNotFoundException"/> with the specified message if the condition is <c>true</c>.
    /// </summary>
    /// <param name="condition">The condition to evaluate. If <c>true</c>, the exception is thrown.</param>
    /// <param name="message">The interpolated message to include in the exception if thrown.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="condition"/> is <c>true</c>.</exception>
    internal static void KeyNotFound(bool condition, [InterpolatedStringHandlerArgument("condition")] ref ConditionInterpolation message)
    {
        if (condition)
        {
            throw new KeyNotFoundException(message.ToString());
        }
    }
}
