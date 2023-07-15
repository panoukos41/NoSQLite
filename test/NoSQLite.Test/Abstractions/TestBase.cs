using System.Runtime.CompilerServices;

namespace NoSQLite.Test.Abstractions;

public abstract class TestBase<TTestClass> : IClassFixture<TestFixture<TTestClass>>
{
    protected TestFixture<TTestClass> Fixture { get; }

    public TestBase(TestFixture<TTestClass> fixture)
    {
        Fixture = fixture;
    }

    public NoSQLiteTable GetTable([CallerMemberName] string? caller = null)
    {
        return Fixture.Connection.GetTable($"{caller}_{Guid.NewGuid()}");
    }

    public NoSQLiteTable GetTable<T>(IDictionary<string, T> initPairs, [CallerMemberName] string? caller = null)
    {
        var table = GetTable(caller);
        table.InsertMany(initPairs);
        return table;
    }
}
