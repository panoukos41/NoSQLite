using BenchmarkDotNet.Attributes;
using NoSQLite;

namespace ConsoleApp;

[MemoryDiagnoser(true)]
public class Benchy
{
    public NoSQLiteConnection Connection { get; set; } = null!;
    public NoSQLiteTable WriteTable { get; set; } = null!;
    public NoSQLiteTable ReadTable { get; set; } = null!;

    public Person Person { get; set; } = null!;
    public string[] PeopleIds { get; set; } = null!;
    public Dictionary<string, Person> People { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        Connection = new(Path.Combine(Environment.CurrentDirectory, "benchmark.sqlite3"));
        WriteTable = Connection.GetTable("Write");
        ReadTable = Connection.GetTable("Read");

        Person = new Person
        {
            Name = "singe_write",
            Description = "A"
        };

        PeopleIds = (0..100).Select(static num => num.ToString()).ToArray();

        People = PeopleIds.Select(num => new Person
        {
            Name = num.ToString(),
            Description = "Yay",
            Surname = "no"
        })
        .ToDictionary(static x => x.Name);

        ReadTable.InsertMany(People);
    }

    [GlobalCleanup]
    public void Cleanup() => Connection.Dispose();

    [Benchmark]
    public Person Read_Singe() => ReadTable.Find<Person>("0")!;

    [Benchmark]
    public List<Person> Read_Many() => ReadTable.FindMany<Person>(PeopleIds).ToList();

    [Benchmark]
    public void Write_Singe() => WriteTable.Insert(Person.Name, Person);

    [Benchmark]
    public void Write_Many() => WriteTable.InsertMany(People);
}
