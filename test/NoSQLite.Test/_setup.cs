using SQLitePCL;
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

public abstract class TestBase
{
    protected string DbPath { get; private set; } = null!;

    protected sqlite3 Db { get; private set; } = null!;

    protected NoSQLiteConnection Connection { get; private set; } = null!;

    protected virtual JsonSerializerOptions? JsonOptions { get; }

    private bool Delete { get; } = false;

    [Before(HookType.Test)]
    public async Task BeforeAsync()
    {
        var dir = Path.Combine(Environment.CurrentDirectory, "databases");
        var now = TimeProvider.System.GetTimestamp();
        DbPath = Path.Combine(dir, $"{GetType().Name}_{now}.sqlite3");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        if (Delete && File.Exists(DbPath))
        {
            File.Delete(DbPath);
        }

        Batteries_V2.Init();
        Db = sqlite3.Create(DbPath, useWal: true);
        Connection = new NoSQLiteConnection(Db, JsonOptions);

        await That(File.Exists(DbPath)).IsTrue();
    }

    public NoSQLiteTable GetTable([CallerMemberName] string? caller = null)
    {
        return Connection.GetTable($"{caller}");
    }

    [After(HookType.Test)]
    public async Task AfterAsync()
    {
        Connection.Dispose();
        Db.CloseAndDispose();
        await That(Connection.Tables.Count).IsEqualTo(0);
        await That(File.Exists($"{DbPath}-shm")).IsFalse();
        await That(File.Exists($"{DbPath}-wal")).IsFalse();

        if (Delete)
        {
            File.Delete(DbPath);
        }
    }
}
