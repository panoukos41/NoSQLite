using NoSQLite.Test.Data;

namespace NoSQLite.Test.Collections;

// Defining the collection and which Fixture to use for it.
[CollectionDefinition(nameof(MainCollection))]
public sealed class MainCollectionDefinition : ICollectionFixture<MainFixture>
{
}

// Main class from which all tests inherit to be executed in the same context.
[Collection(nameof(MainCollection))]
public abstract class MainCollection
{
    public MainCollection(MainFixture fixture)
    {
        Db = fixture.Connection.GetTable();
    }

    public NoSQLiteTable Db { get; }

    // todo: Provide random people generation or many random people.

    public Person Person { get; } = new()
    {
        Name = "Anakin",
        Surname = "Skywalker",
        Phone = "",
        Email = "anakin_skywalker@lowground.com",
        Birthdate = DateTimeOffset.UtcNow
    };
}

// Shared data for all tests of MainCollection.
public sealed class MainFixture : IAsyncLifetime
{
    public MainFixture()
    {
        Connection = new NoSQLiteConnection(Path.Combine(Environment.CurrentDirectory, "test.sqlite3"));
    }

    public NoSQLiteConnection Connection { get; }

    public Task InitializeAsync()
    {
        Assert.True(File.Exists(Connection.Path));
        // todo: Create a lot of people data.
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Connection.Dispose();
        return Task.CompletedTask;
    }
}
