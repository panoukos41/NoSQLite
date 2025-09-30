namespace NoSQLite.Test;

public sealed class Indexing : TestBase<Indexing>
{
    [Test]
    public async Task Exists()
    {
        var table = GetTable();

        var create = table.CreateIndex("test", "email");
        await That(create).IsTrue();

        var exists = table.IndexExists("test");
        var notExists = table.IndexExists("test_1");

        await That(exists).IsTrue();
        await That(notExists).IsFalse();
    }

    [Test]
    public async Task Create()
    {
        var table = GetTable();

        var notExists = table.CreateIndex("test", "email");
        var exists = table.CreateIndex("test", "email");

        await That(exists).IsTrue();
        await That(notExists).IsFalse();
    }

    [Test]
    public async Task Recreate()
    {
        var table = GetTable();

        table.RecreateIndex("test", "email");

        await That(table.IndexExists("test")).IsTrue();
    }

    [Test]
    public async Task Delete()
    {
        var table = GetTable();

        var create = table.CreateIndex("test", "email");
        await That(create).IsTrue();

        var exists = table.DeleteIndex("test");
        var notExists = table.DeleteIndex("test");

        await That(exists).IsTrue();
        await That(notExists).IsFalse();
    }
}
