namespace NoSQLite.Test;

[MethodDataSource<Table>(nameof(Arguments))]
public sealed class Table : TestBase
{
    public static IEnumerable<Func<JsonSerializerOptions?>> Arguments() =>
    [
        () => null,
        () => JsonSerializerOptions.Default,
        () => new(JsonSerializerOptions.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull },
    ];

    public Table(JsonSerializerOptions? jsonOptions) : base(jsonOptions)
    {
    }

    [Test]
    public async Task CRUD()
    {
        var people = new PersonFaker().Generate(10);
        var table = Connection.GetTable("crud");

        // Insert
        foreach (var person in people)
        {
            table.Add(person);
        }

        // Count, LongCount, All
        await That(table.Count()).IsEqualTo(10);
        await That(table.LongCount()).IsEqualTo(10);
        await That(table.All<TestPerson>().Length).IsEqualTo(10);

        // Exists
        var id = 5;
        await That(table.Exists<TestPerson, int>(p => p.Id, id)).IsTrue();
        await That(table.Exists<TestPerson, int>(p => p.Id, 15)).IsFalse();

        // Find
        await That(() => table.Find<TestPerson, int>(p => p.Id, 15)).Throws<NoSQLiteException>().WithInnerException().And.IsTypeOf<KeyNotFoundException>();
        var person5 = await That(table.Find<TestPerson, int>(p => p.Id, id)).IsNotNull();

        // Update
        person5!.Name = "test";
        table.Update(person5, p => p.Id);

        // Select
        await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Name, id)).IsEqualTo("test");

        // Insert/Replace/Set
        // Check conditions for where the value doesnt exist works only when options are set to not writting null
        if (Connection.JsonOptions?.DefaultIgnoreCondition is JsonIgnoreCondition.WhenWritingNull)
        {
            // should not replace because it doesnt exist
            table.Replace<TestPerson, int, string>(p => p.Id, p => p.Nonce, id, "r");
            await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Nonce, id)).IsEqualTo(null);

            // should insert because it doesnt exist
            table.Insert<TestPerson, int, string>(p => p.Id, p => p.Nonce, id, "inserted");
            await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Nonce, id)).IsEqualTo("inserted");

            // should replace beacuse it exists
            table.Replace<TestPerson, int, string>(p => p.Id, p => p.Nonce, id, "replaced");
            await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Nonce, id)).IsEqualTo("replaced");

            // should set the value even if it deosnt exist
            table.Set<TestPerson, int, string>(p => p.Id, p => p.Nonce2, id, "aaaaa");
            await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Nonce2, id)).IsEqualTo("aaaaa");
        }

        // Replace
        table.Replace<TestPerson, int, bool>(p => p.Id, p => p.Sane, id, false);
        await That(table.FindProperty<TestPerson, int, bool>(p => p.Id, p => p.Sane, id)).IsFalse();

        // Set (should set regardles of existances or not)
        table.Set<TestPerson, int, string>(p => p.Id, p => p.Name, id, "test from set");
        await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Name, id)).IsEqualTo("test from set");

        // Delete, (Assert) Exists, Count, LongCount, All
        table.Delete<TestPerson, int>(p => p.Id, id);
        await That(table.Exists<TestPerson, int>(p => p.Id, id)).IsFalse();
        await That(table.Count()).IsEqualTo(9);
        await That(table.LongCount()).IsEqualTo(9);
        await That(table.All<TestPerson>().Length).IsEqualTo(9);

        // Clear, (Assert) Count, LongCount, All
        table.Clear();
        await That(table.Count()).IsEqualTo(0);
        await That(table.LongCount()).IsEqualTo(0);
        await That(table.All<TestPerson>().Length).IsEqualTo(0);
    }

    [Test]
    [Arguments("MyIndex")]
    [Arguments("123 My Index")]
    [Arguments("!@# -- __ -- // --")]
    [Arguments(" ")]
    [Arguments("")]
    public async Task Index(string indexName)
    {
        var tableName = "index";
        var table = Connection.GetTable(tableName);
        var propertyPath = Extensions.GetPropertyPath<TestPerson, int>(p => p.Id, Connection.JsonOptions);

        await That(table.IndexExists(indexName)).IsFalse();

        table.CreateIndex<TestPerson, int>(p => p.Id, indexName);

        await That(table.IndexExists(indexName)).IsTrue();

        // test plan index
        using var planStmt = table.NewStmt($"""
            EXPLAIN QUERY PLAN
            SELECT *
            FROM "{tableName}"
            WHERE "documents"->'$.{propertyPath}' = '10';
            """);

        var result = planStmt.Execute(null, r => r.Text(3));
        await That(result).IsEqualTo($"SEARCH {tableName} USING INDEX {tableName}_{indexName} (<expr>=?)");

        table.DeleteIndex(indexName);

        await That(table.IndexExists(indexName)).IsFalse();
    }

    [Test]
    public async Task Index_Unique()
    {
        var indexName = "id";
        var tableName = "unique";
        var table = Connection.GetTable(tableName);
        var propertyPath = Extensions.GetPropertyPath<TestPerson, int>(p => p.Id, Connection.JsonOptions);

        await That(table.IndexExists(indexName)).IsFalse();

        table.CreateIndex<TestPerson, int>(p => p.Id, indexName, unique: true);

        await That(table.IndexExists(indexName)).IsTrue();

        // test plan index
        using var planStmt = table.NewStmt($"""
            EXPLAIN QUERY PLAN
            SELECT *
            FROM "{tableName}"
            WHERE "documents"->'$.{propertyPath}' = '10';
            """);

        var result = planStmt.Execute(null, r => r.Text(3));
        await That(result).IsEqualTo($"SEARCH {tableName} USING INDEX {tableName}_{indexName} (<expr>=?)");

        // insert two times
        var personFaker = new PersonFaker();
        var person = personFaker.Generate();
        await That(() => table.Add(person)).ThrowsNothing();

        // second time throws
        await That(() => table.Add(person)).Throws<NoSQLiteException>();

        // count remains only one person
        await That(table.Count()).IsEqualTo(1);

        // delete index
        table.DeleteIndex(indexName);
        await That(table.IndexExists(indexName)).IsFalse();

        // insert doesnt throw
        await That(() => table.Add(person)).ThrowsNothing();

        // count goes up to two people
        await That(table.Count()).IsEqualTo(2);
    }
}
