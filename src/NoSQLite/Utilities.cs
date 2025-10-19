using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NoSQLite;

/// <summary>
/// Provides extension methods for SQLite result checking.
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// Checks the SQLite result code and throws a <see cref="NoSQLiteException"/> if the result indicates an error.
    /// </summary>
    /// <param name="db">The SQLite database handle.</param>
    /// <param name="result">The SQLite result code to check.</param>
    /// <param name="message">The interpolated message to include in the exception if thrown.</param>
    /// <returns>The original <paramref name="result"/> if no error is detected.</returns>
    /// <exception cref="NoSQLiteException">Thrown when the result code indicates an error.</exception>
    public static int CheckResult(this sqlite3 db, int result, [InterpolatedStringHandlerArgument("result")] ref SQLiteCodeInterpolation message)
    {
        if (message.ShouldThrow)
        {
            throw new NoSQLiteException($"{message.ToString()}. SQLite info, code: {result}, message: {sqlite3_errmsg(db).utf8_to_string()}");
        }
        return result;
    }

    /// <summary>
    /// Checks the SQLite result code and throws a <see cref="NoSQLiteException"/> if the result indicates an error.
    /// </summary>
    /// <param name="result">The SQLite result code to check.</param>
    /// <param name="db">The SQLite database handle.</param>
    /// <param name="message">The interpolated message to include in the exception if thrown.</param>
    /// <returns>The original <paramref name="result"/> if no error is detected.</returns>
    /// <exception cref="NoSQLiteException">Thrown when the result code indicates an error.</exception>
    public static int CheckResult(this int result, sqlite3 db, [InterpolatedStringHandlerArgument("result")] ref SQLiteCodeInterpolation message)
    {
        if (message.ShouldThrow)
        {
            throw new NoSQLiteException($"{message.ToString()}. SQLite info, code: {result}, message: {sqlite3_errmsg(db).utf8_to_string()}");
        }
        return result;
    }

    /// <summary>
    /// Checks the SQLite result code and throws a <see cref="NoSQLiteException"/> if the result indicates an error.
    /// </summary>
    /// <param name="result">The SQLite result code to check.</param>
    /// <param name="connection">The <see cref="NoSQLiteConnection"/> instance associated with the operation.</param>
    /// <param name="message">The interpolated message to include in the exception if thrown.</param>
    /// <returns>The original <paramref name="result"/> if no error is detected.</returns>
    /// <exception cref="NoSQLiteException">Thrown when the result code indicates an error.</exception>
    public static int CheckResult(this int result, NoSQLiteConnection connection, [InterpolatedStringHandlerArgument("result")] ref SQLiteCodeInterpolation message)
    {
        if (message.ShouldThrow)
        {
            throw new NoSQLiteException($"{message.ToString()}. SQLite info, code: {result}, message: {sqlite3_errmsg(connection.db).utf8_to_string()}");
        }
        return result;
    }

    /// <summary>
    /// Extracts the property name from a lambda expression representing a property accessor.
    /// </summary>
    /// <typeparam name="T">The type containing the property.</typeparam>
    /// <typeparam name="TKey">The type of the property.</typeparam>
    /// <param name="expression">An expression representing a property accessor, e.g., <c>x => x.Property</c>.</param>
    /// <returns>The name of the property accessed in the expression.</returns>
    /// <exception cref="ArgumentException">Thrown when the expression does not represent a property access.</exception>
    public static string GetPropertyName<T, TKey>(this Expression<Func<T, TKey>> expression, JsonSerializerOptions? jsonOptions)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return jsonOptions?.PropertyNamingPolicy?.ConvertName(memberExpression.Member.Name) ?? memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression operand)
        {
            return jsonOptions?.PropertyNamingPolicy?.ConvertName(operand.Member.Name) ?? operand.Member.Name;
        }

        throw new ArgumentException("Invalid expression. Expected a property access expression.", nameof(expression));
    }

    /// <summary>
    /// Extracts the full property path from a lambda expression representing a property accessor.
    /// </summary>
    /// <typeparam name="T">The type containing the property.</typeparam>
    /// <typeparam name="TKey">The type of the property.</typeparam>
    /// <param name="expression">An expression representing a property accessor, e.g., <c>x => x.Nested.Property</c>.</param>
    /// <returns>The full path of the property accessed in the expression, e.g., "Nested.Property".</returns>
    /// <exception cref="ArgumentException">Thrown when the expression does not represent a property access.</exception>
    public static string GetPropertyPath<T, TKey>(this Expression<Func<T, TKey>> expression, JsonSerializerOptions? jsonOptions)
    {
        static string BuildPath(Expression? expr)
        {
            if (expr is MemberExpression memberExpression)
            {
                var parentPath = BuildPath(memberExpression.Expression);
                return string.IsNullOrEmpty(parentPath)
                    ? memberExpression.Member.Name
                    : $"{parentPath}.{memberExpression.Member.Name}";
            }

            if (expr is UnaryExpression unaryExpression)
            {
                return BuildPath(unaryExpression.Operand);
            }

            return string.Empty;
        }

        var path = BuildPath(expression.Body);
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Invalid expression. Expected a property access expression.", nameof(expression));
        }

        return jsonOptions?.PropertyNamingPolicy?.ConvertName(path) ?? path;
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
