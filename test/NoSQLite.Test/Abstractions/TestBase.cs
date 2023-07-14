using System.Runtime.CompilerServices;

namespace NoSQLite.Test.Abstractions;

public abstract class TestBase<TTestClass> : IClassFixture<TestFixture<TTestClass>>
{
    private readonly TestFixture<TTestClass> fixture;

    public TestBase(TestFixture<TTestClass> fixture)
    {
        this.fixture = fixture;
    }

    public NoSQLiteTable GetTable([CallerMemberName] string? caller = null)
    {
        return fixture.Connection.GetTable($"{caller}_{Guid.NewGuid()}");
    }

    public NoSQLiteTable GetTable<T>(IDictionary<string, T> initPairs, [CallerMemberName] string? caller = null)
    {
        var table = GetTable(caller);
        table.InsertMany(initPairs);
        return table;
    }
}
