namespace NoSQLite.Test.Abstractions;

public sealed class TestFixture<TTestClass> : IAsyncLifetime
{
    public string DbPath { get; private set; } = null!;

    public NoSQLiteConnection Connection { get; private set; } = null!;

    public Task InitializeAsync()
    {
        DbPath = Path.Combine(Environment.CurrentDirectory, $"{typeof(TTestClass).Name}.sqlite3");
        Connection = new NoSQLiteConnection(DbPath);

        Assert.True(File.Exists(Connection.Path));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Connection.Dispose();
        Assert.Equal(0, Connection.Tables.Count);

        File.Delete(DbPath);
        return Task.CompletedTask;
    }
}
