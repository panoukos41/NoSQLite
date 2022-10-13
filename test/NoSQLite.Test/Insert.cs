namespace NoSQLite.Test;

public class Insert : MainCollection
{
    public Insert(MainFixture fixture) : base(fixture)
    {
    }

    // should insert or replace the document for the provided key.
    // if an exception is thrown from System.Text.Json should throw
    // if the json is not valid should throw. (returned by the db if not valid)
}
