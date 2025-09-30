using System.Runtime.CompilerServices;

namespace NoSQLite.Test;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Randomizer.Seed = new Random(420690001);
    }
}

public abstract class TestBase<TSelf>
{
    public static string DbPath { get; private set; } = null!;

    public static NoSQLiteConnection Connection { get; private set; } = null!;

    public static bool Delete { get; set; } = true;

    [Before(Class)]
    public static async Task BeforeAsync()
    {
        DbPath = Path.Combine(Environment.CurrentDirectory, $"{typeof(TSelf).Name}.sqlite3");

        if (File.Exists(DbPath))
        {
            File.Delete(DbPath);
        }

        Connection = new NoSQLiteConnection(DbPath);

        await That(File.Exists(Connection.Path)).IsTrue();
    }

    public static NoSQLiteTable GetTable([CallerMemberName] string? caller = null)
    {
        return Connection.GetTable($"{caller}_{Guid.NewGuid()}");
    }

    public static NoSQLiteTable GetTable<T>(IDictionary<string, T> initPairs, [CallerMemberName] string? caller = null)
    {
        var table = GetTable(caller);
        table.InsertMany(initPairs);
        return table;
    }

    [After(Assembly)]
    public static async Task AfterAsync()
    {
        Connection.Dispose();
        await That(Connection.Tables.Count).IsEqualTo(0);

        if (Delete)
        {
            File.Delete(DbPath);
        }
    }
}
