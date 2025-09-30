namespace NoSQLite.Test;

public sealed class Operations : TestBase<Operations>
{
    [Test]
    public async Task CreateTable()
    {
        var table = nameof(CreateTable);

        await That(Connection.TableExists(table)).IsFalse();

        Connection.CreateTable(table);

        await That(Connection.TableExists(table)).IsTrue();
    }

    [Test]
    public async Task DropTable()
    {
        var table = nameof(DropTable);

        await That(Connection.TableExists(table)).IsFalse();

        Connection.CreateTable(table);

        await That(Connection.TableExists(table)).IsTrue();

        Connection.DropTable(table);

        await That(Connection.TableExists(table)).IsFalse();
    }

    [Test]
    public async Task DropAndCreateTable()
    {
        var table = nameof(DropAndCreateTable);

        await That(Connection.TableExists(table)).IsFalse();

        Connection.CreateTable(table);

        await That(Connection.TableExists(table)).IsTrue();

        var dbTable = Connection.GetTable(table);
        dbTable.Insert("0", new PersonFaker().Generate());
        await That(dbTable.Count()).IsEqualTo(1);
        dbTable.Dispose();
        dbTable = null;

        Connection.DropAndCreateTable(table);
        await That(Connection.TableExists(table)).IsTrue();

        dbTable = Connection.GetTable(table);
        await That(dbTable.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Checkpoint()
    {
        var dir = Path.Combine(Environment.CurrentDirectory, $"{nameof(Operations)}_{nameof(Checkpoint)}_dir");
        var path = Path.Combine(dir, $"db.sqlite3");

        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var connection = new NoSQLiteConnection(path);

        await That(Directory.GetFiles(dir).Length).IsEqualTo(1);

        var table = connection.GetTable();
        table.Insert("0", new PersonFaker().Generate());

        await That(Directory.GetFiles(dir).Length).IsEqualTo(3);

        connection.Checkpoint();

        await That(Directory.GetFiles(dir).Length).IsEqualTo(1);

        connection.Dispose();
        Directory.Delete(dir, true);
    }

    [Test]
    public async Task Dispose_Test()
    {
        var dir = Path.Combine(Environment.CurrentDirectory, $"{nameof(Operations)}_Dispose_dir");
        var path = Path.Combine(dir, $"db.sqlite3");

        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var connection = new NoSQLiteConnection(path);

        await That(Directory.GetFiles(dir).Length).IsEqualTo(1);

        var table = connection.GetTable();
        table.Insert("0", new PersonFaker().Generate());

        await That(Directory.GetFiles(dir).Length).IsEqualTo(3);

        connection.Dispose();

        await That(Directory.GetFiles(dir).Length).IsEqualTo(1);

        Directory.Delete(dir, true);
    }
}
