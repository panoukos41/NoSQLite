using NoSQLite;

namespace ConsoleApp;

public static class INDEX
{
    public static void Execute(NoSQLiteConnection db)
    {
        var indexName = "name_index";

        var exists = db.Indexes.Exists(indexName);

        db.Indexes.Create(indexName, "name");

        exists = db.Indexes.Exists(indexName);

        db.Indexes.Delete(indexName);

        exists = db.Indexes.Exists(indexName);
    }
}
