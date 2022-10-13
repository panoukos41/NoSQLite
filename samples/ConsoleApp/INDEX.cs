using NoSQLite;

namespace ConsoleApp;

public static class INDEX
{
    public static void Execute(NoSQLiteConnection db)
    {
        var indexName = "name_index";

        var exists = db.IndexExists(indexName);

        db.CreateIndex(indexName, "name");

        exists = db.IndexExists(indexName);

        db.DeleteIndex(indexName);

        exists = db.IndexExists(indexName);
    }
}
