using System.Text.Json;

namespace NoSQLite.Test;

public sealed class CRUD : TestBase<CRUD>
{
    private readonly Dictionary<string, TestPerson> seed;

    public CRUD()
    {
        seed = new PersonFaker().Generate(10).ToDictionary(x => x.Email);
    }

    [Test]
    public async Task Insert()
    {
        var table = GetTable();

        table.Insert("0", seed.First());
    }

    [Test]
    public async Task InsertMany()
    {
        var table = GetTable();
        var peopleBytes = seed.ToDictionary(x => x.Key, pair => JsonSerializer.SerializeToUtf8Bytes(pair.Value));

        table.InsertMany(seed);
        table.InsertBytesMany(peopleBytes);
    }

    [Test]
    public async Task Count()
    {
        var table = GetTable(seed);

        var count = table.Count();
        var longCount = table.LongCount();

        await That(count).IsEqualTo(seed.Count);
        await That(longCount).IsEqualTo(seed.Count);
    }

    [Test]
    public async Task All()
    {
        var table = GetTable(seed);

        var people = table.All<Person>();
        var peopleBytes = table.AllBytes();

        await That(people).IsEquivalentTo(seed.Values);
        await That(peopleBytes.Select(p => JsonSerializer.Deserialize<Person>(p))).IsEquivalentTo(seed.Values);
    }

    [Test]
    public async Task Exists()
    {
        var table = GetTable(seed);
        var person = seed.First();

        var exists = table.Exists(person.Key);

        await That(exists).IsTrue();
    }

    [Test]
    public async Task Find()
    {
        var table = GetTable(seed);
        var pair = seed.First();
        var person = pair.Value;

        var found = table.Find<TestPerson>(pair.Key);
        var foundBytes = table.FindBytes(pair.Key);

        await That(found).IsEqualTo(person);
        await That(JsonSerializer.Deserialize<TestPerson>(foundBytes)).IsEqualTo(person);
    }

    [Test]
    public async Task FindMany()
    {
        var table = GetTable(seed);

        var people = table.FindMany<Person>(seed.Keys);
        var peopleBytes = table.FindBytesMany(seed.Keys);

        await That(people).IsEquivalentTo(seed.Values);
        await That(peopleBytes.Select(p => JsonSerializer.Deserialize<Person>(p))).IsEquivalentTo(seed.Values);
    }

    [Test]
    public async Task FindPairs()
    {
        var table = GetTable(seed);

        var people = table.FindPairs<Person>(seed.Keys);
        var peopleBytes = table.FindBytesPairs(seed.Keys);

        await That(people).IsEquivalentTo(seed);
        await That(peopleBytes.ToDictionary(p => p.Key, p => JsonSerializer.Deserialize<Person>(p.Value))).IsEquivalentTo(seed);
    }

    [Test]
    public async Task Clear()
    {
        var table = GetTable(seed);

        await That(table.Count()).IsNotEqualTo(0);

        table.Clear();

        await That(table.Count()).IsNotEqualTo(0);
    }
}
