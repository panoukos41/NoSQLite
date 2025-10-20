using System.Diagnostics.CodeAnalysis;
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
    internal NoSQLiteException(string message, Exception inner) : base(message, inner)
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
    /// Throws a <see cref="KeyNotFoundException"/> with a message indicating that a document for the specified key was not found,
    /// if the provided <paramref name="item"/> is <c>null</c>.
    /// </summary>
    /// <typeparam name="T">The type of the item being checked.</typeparam>
    /// <typeparam name="TKey">The type of the key used to identify the item.</typeparam>
    /// <param name="item">The item to check for existence. If <c>null</c>, the exception is thrown.</param>
    /// <param name="key">The key associated with the item.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    internal static void KeyNotFound<T, TKey>([NotNull] T? item, TKey key)
    {
        if (item is null)
        {
            var msg = $"Could not find document for key {key}";
            throw new NoSQLiteException(msg, new KeyNotFoundException(msg));
        }
    }
}
