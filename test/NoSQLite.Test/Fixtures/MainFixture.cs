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
        Db = fixture.Db;
    }

    public NoSQLiteConnection Db { get; }

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
        Db = new NoSQLiteConnection(Path.Combine(Environment.CurrentDirectory, "test.sqlite3"));
    }

    public NoSQLiteConnection Db { get; }

    public Task InitializeAsync()
    {
        Assert.True(File.Exists(Db.Path));
        // todo: Create a lot of people data.
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Db.Dispose();
        return Task.CompletedTask;
    }
}
