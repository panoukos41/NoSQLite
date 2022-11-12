namespace NoSQLite.Test;

public class Remove : MainCollection
{
    public Remove(MainFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Item_that_exists()
    {
        var id = "item_that_exists";

        Db.Insert(id, Person);

        Db.Exists(id).Should().BeTrue();

        Db.Remove(id);

        Db.Exists(id).Should().BeFalse();
    }

    [Fact]
    public void Item_that_is_not_found_should_not_throw()
    {
        var func = () => Db.Remove("person");

        func.Should().NotThrow();
    }

    [Fact]
    public void Many_items_that_exist_and_dont()
    {
        Db.InsertMany(new Dictionary<string, Person>
        {
            ["many_1"] = Person,
            ["many_2"] = Person,
            ["many_3"] = Person,
        });

        var func = () => Db.RemoveMany(new[] { "many_1", "many_2", "many_3", "not_exist_1", "not_exist_2" });

        func.Should().NotThrow();

        Db.Exists("many_1").Should().BeFalse();
        Db.Exists("many_2").Should().BeFalse();
        Db.Exists("many_3").Should().BeFalse();
    }
}
