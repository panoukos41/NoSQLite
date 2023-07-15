namespace NoSQLite.Test;

public sealed class Indexing : TestBase<Indexing>
{
    public Indexing(TestFixture<Indexing> fixture) : base(fixture)
    {
    }

    [Fact]
    public void Exists()
    {
        var table = GetTable();

        var create = table.CreateIndex("test", "email");
        create.Should().BeTrue();

        var exists = table.IndexExists("test");
        var notExists = table.IndexExists("test_1");

        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public void Create()
    {
        var table = GetTable();

        var notExists = table.CreateIndex("test", "email");
        var exists = table.CreateIndex("test", "email");

        notExists.Should().BeTrue();
        exists.Should().BeFalse();
    }

    [Fact]
    public void Recreate()
    {
        var table = GetTable();

        table.RecreateIndex("test", "email");

        table.IndexExists("test").Should().BeTrue();
    }

    [Fact]
    public void Delete()
    {
        var table = GetTable();

        var create = table.CreateIndex("test", "email");
        create.Should().BeTrue();

        var exists = table.DeleteIndex("test");
        var notExists = table.DeleteIndex("test");

        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }
}
