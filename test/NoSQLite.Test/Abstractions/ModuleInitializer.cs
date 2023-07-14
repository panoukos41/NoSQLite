using System.Runtime.CompilerServices;

namespace NoSQLite.Test.Collections;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Bogus.Randomizer.Seed = new Random(420690001);
    }
}
