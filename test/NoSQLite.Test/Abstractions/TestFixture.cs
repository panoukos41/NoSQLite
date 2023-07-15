namespace NoSQLite.Test.Abstractions;

public sealed class TestFixture<TTestClass> : IAsyncLifetime
{
    public string Path { get; private set; } = null!;

    public NoSQLiteConnection Connection { get; private set; } = null!;

    public bool Delete { get; set; } = true;

    public Task InitializeAsync()
    {
        Path = System.IO.Path.Combine(Environment.CurrentDirectory, $"{typeof(TTestClass).Name}.sqlite3");

        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        Connection = new NoSQLiteConnection(Path);

        Assert.True(File.Exists(Connection.Path));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Connection.Dispose();
        Assert.Equal(0, Connection.Tables.Count);

        if (Delete)
        {
            File.Delete(Path);
        }
        return Task.CompletedTask;
    }
}
