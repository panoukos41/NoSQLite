using System.Text.Json;

namespace NoSQLite.Test;

public class CRUD : TestBase<CRUD>
{
    private readonly Dictionary<string, Person> seed;

    public CRUD(TestFixture<CRUD> fixture) : base(fixture)
    {
        seed = new PersonFaker().Generate(10).ToDictionary(x => x.Email);
    }

    [Fact]
    public void Insert()
    {
        var table = GetTable();

        table.Insert("0", seed.First());
    }

    [Fact]
    public void InsertMany()
    {
        var table = GetTable();
        var peopleBytes = seed.ToDictionary(x => x.Key, pair => JsonSerializer.SerializeToUtf8Bytes(pair.Value));

        table.InsertMany(seed);
        table.InsertBytesMany(peopleBytes);
    }

    [Fact]
    public void Count()
    {
        var table = GetTable(seed);

        var count = table.Count();
        var longCount = table.LongCount();

        count.Should().Be(seed.Count);
        longCount.Should().Be(seed.Count);
    }

    [Fact]
    public void All()
    {
        var table = GetTable(seed);

        var people = table.All<Person>();
        var peopleBytes = table.AllBytes();

        people.Should().BeEquivalentTo(seed.Values);
        peopleBytes.Select(p => JsonSerializer.Deserialize<Person>(p)).Should().BeEquivalentTo(seed.Values);
    }

    [Fact]
    public void Exists()
    {
        var table = GetTable(seed);
        var person = seed.First();

        var exists = table.Exists(person.Key);

        exists.Should().BeTrue();
    }

    [Fact]
    public void Find()
    {
        var table = GetTable(seed);
        var pair = seed.First();
        var person = pair.Value;

        var found = table.Find<Person>(pair.Key);
        var foundBytes = table.FindBytes(pair.Key);

        found.Should().Be(person);
        JsonSerializer.Deserialize<Person>(foundBytes).Should().Be(person);
    }

    [Fact]
    public void FindMany()
    {
        var table = GetTable(seed);

        var people = table.FindMany<Person>(seed.Keys);
        var peopleBytes = table.FindBytesMany(seed.Keys);

        people.Should().BeEquivalentTo(seed.Values);
        peopleBytes.Select(p => JsonSerializer.Deserialize<Person>(p)).Should().BeEquivalentTo(seed.Values);
    }

    [Fact]
    public void FindPairs()
    {
        var table = GetTable(seed);

        var people = table.FindPairs<Person>(seed.Keys);
        var peopleBytes = table.FindBytesPairs(seed.Keys);

        people.Should().BeEquivalentTo(seed);
        peopleBytes.ToDictionary(p => p.Key, p => JsonSerializer.Deserialize<Person>(p.Value)).Should().BeEquivalentTo(seed);
    }
}
