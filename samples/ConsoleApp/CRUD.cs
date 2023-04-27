using NoSQLite;

namespace ConsoleApp;

public static class CRUD
{
    public static void Objects(NoSQLiteConnection connection)
    {
        var db = connection.GetTable();
        // objects
        var panos = new Person
        {
            Name = "panos",
            Surname = "athanasiou"
        };

        var john = new Person
        {
            Name = "john",
            Surname = "mandis",
            Description = "good friendo!"
        };

        db.Insert("0", panos);
        db.Insert("1", john);

        db.InsertMany(new Dictionary<string, Person>
        {
            ["0"] = panos,
            ["1"] = john
        });

        var exists = db.Exists("0");

        var panos2 = db.Find<Person>("0");
        var john2 = db.Find<Person>("1");

        var people = db.FindMany<Person>(new[] { "0", "1" }).ToArray();

        db.Remove("0");
        db.RemoveMany(new[] { "0", "1" });

        db.Insert("0", panos);
        db.Insert("1", john);
    }

    public static void Lists(NoSQLiteConnection connection)
    {
        var db = connection.GetTable();

        var people = new[]
        {
            new Person{ Name = "person1", },
            new Person{ Name = "person2", },
            new Person{ Name = "person3", },
            new Person{ Name = "person4", },
        };

        db.Insert("people", people);

        var exists = db.Exists("people");

        var people2 = db.Find<Person[]>("people");

        db.Remove("people");

        db.Insert("people", people);
    }
}
