using NoSQLite.Test.Data;

namespace NoSQLite.Test.Collections;

[CollectionDefinition(nameof(MainCollection))]
public sealed class MainCollectionDefinition : ICollectionFixture<MainFixture>
{
}

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

public sealed class MainFixture : IDisposable
{
    public MainFixture()
    {
        Db = new NoSQLiteConnection(Path.Combine(Environment.CurrentDirectory, "test.sqlite3"));
    }

    public NoSQLiteConnection Db { get; }

    public void Dispose()
    {
        Db.Dispose();
    }
}
