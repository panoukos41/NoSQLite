namespace NoSQLite.Test;

public sealed class Table : TestBase
{
    [Test]
    public async Task CRUD()
    {
        var people = new PersonFaker().Generate(10);
        var table = Connection.GetTable("crud");

        // Insert
        foreach (var person in people)
        {
            table.Insert(person);
        }

        // Count, LongCount, All
        await That(table.Count()).IsEqualTo(10);
        await That(table.LongCount()).IsEqualTo(10);
        await That(table.All<TestPerson>().Length).IsEqualTo(10);

        // Exists
        var id = 5;
        await That(table.Exists<TestPerson, int>(p => p.Id, id)).IsTrue();

        // Find
        var person5 = await That(table.Find<TestPerson, int>(p => p.Id, id)).IsNotNull();

        // Update
        person5.Name = "test";
        table.Update(person5, p => p.Id);

        // Select (todo)
        await That(table.FindProperty<TestPerson, int, string>(p => p.Id, p => p.Name, id)).IsEqualTo("test");

        // Remove, (Assert) Exists, Count, LongCount, All
        table.Remove<TestPerson, int>(p => p.Id, id);
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
        var table = Connection.GetTable("indexes");

        await That(table.IndexExists(indexName)).IsFalse();

        table.CreateIndex<TestPerson, int>(p => p.Id, indexName);

        await That(table.IndexExists(indexName)).IsTrue();

        // test plan index
        using var planStmt = new SQLiteStmt(Db, """
            EXPLAIN QUERY PLAN
            SELECT *
            FROM "indexes"
            WHERE "documents"->'$.Id' = '10';
            """);

        var result = planStmt.Execute(null, r => r.Text(3));
        await That(result).IsEqualTo($"SEARCH indexes USING INDEX indexes_{indexName} (<expr>=?)");

        table.DeleteIndex(indexName);

        await That(table.IndexExists(indexName)).IsFalse();
    }
}
