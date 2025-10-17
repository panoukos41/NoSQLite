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
        var tableName = "index";
        var table = Connection.GetTable(tableName);

        await That(table.IndexExists(indexName)).IsFalse();

        table.CreateIndex<TestPerson, int>(p => p.Id, indexName);

        await That(table.IndexExists(indexName)).IsTrue();

        // test plan index
        using var planStmt = new SQLiteStmt(Db, $"""
            EXPLAIN QUERY PLAN
            SELECT *
            FROM "{tableName}"
            WHERE "documents"->'$.Id' = '10';
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

        await That(table.IndexExists(indexName)).IsFalse();

        table.CreateIndex<TestPerson, int>(p => p.Id, indexName, unique: true);

        await That(table.IndexExists(indexName)).IsTrue();

        // test plan index
        using var planStmt = new SQLiteStmt(Db, $"""
            EXPLAIN QUERY PLAN
            SELECT *
            FROM "{tableName}"
            WHERE "documents"->'$.Id' = '10';
            """);

        var result = planStmt.Execute(null, r => r.Text(3));
        await That(result).IsEqualTo($"SEARCH {tableName} USING INDEX {tableName}_{indexName} (<expr>=?)");

        // insert two times
        var personFaker = new PersonFaker();
        var person = personFaker.Generate();
        await That(() => table.Insert(person)).ThrowsNothing();

        // second time throws
        await That(() => table.Insert(person)).Throws<NoSQLiteException>();

        // count remains only one person
        await That(table.Count()).IsEqualTo(1);

        // delete index
        table.DeleteIndex(indexName);
        await That(table.IndexExists(indexName)).IsFalse();

        // insert doesnt throw
        await That(() => table.Insert(person)).ThrowsNothing();

        // count goes up to two people
        await That(table.Count()).IsEqualTo(2);
    }
}
