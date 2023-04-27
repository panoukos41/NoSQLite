namespace System;

public static class RangeExtensions
{
    public static RangeEnumerator GetEnumerator(this Range range) => new(range);

    public static IEnumerable<TResult> Select<TResult>(this Range range, Func<int, TResult> selector)
    {
        foreach (var num in range)
        {
            yield return selector(num);
        }
    }
}

public struct RangeEnumerator
{
    private int _current;
    private readonly int _end;

    public RangeEnumerator(Range range)
    {
        if (range.End.IsFromEnd) throw new NotSupportedException("From end ranges not supported");

        _current = range.Start.Value - 1;
        _end = range.End.Value;
    }

    public int Current => _current;

    public bool MoveNext()
    {
        _current += 1;
        return _current <= _end;
    }
}
