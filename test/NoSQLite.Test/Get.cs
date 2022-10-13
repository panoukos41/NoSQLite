namespace NoSQLite.Test;

public class Get : MainCollection
{
    public Get(MainFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Item_that_exists()
    {
        var id = "item_that_exists";
        Db.Insert(id, Person);

        var value = Db.Find<Person>(id);
        var bytes = Db.FindBytes(id);

        value.Should().NotBeNull();
        bytes.Should().NotBeNull();
    }

    [Fact]
    public void Item_that_does_not_exist()
    {
        var id = "item_that_does_not_exist";

        var value = Db.Find<Person>(id);
        var bytes = Db.FindBytes(id);

        value.Should().BeNull();
        bytes.Should().BeNull();
    }

    // todo: Test List Get methods.
}
