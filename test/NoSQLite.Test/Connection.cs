namespace NoSQLite.Test;

public sealed class Connection : TestBase
{
    [Test]
    [Arguments("MyDocuments")]
    [Arguments("123 My Documents")]
    [Arguments("!@# -- __ -- // --")]
    [Arguments(" ")]
    [Arguments("")]
    public async Task Create_And_Drop_Table(string tableName)
    {
        await That(Connection.TableExists(tableName)).IsFalse();

        Connection.CreateTable(tableName);

        await That(Connection.TableExists(tableName)).IsTrue();

        Connection.DropTable(tableName);

        await That(Connection.TableExists(tableName)).IsFalse();
    }
}
