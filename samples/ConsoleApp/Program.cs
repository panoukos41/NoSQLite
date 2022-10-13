global using ConsoleApp.Data;
using ConsoleApp;
using NoSQLite;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var stopwatch = Stopwatch.StartNew();

var db = new NoSQLiteConnection(
    Path.Combine(Environment.CurrentDirectory, "console.sqlite3"),
    new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });

stopwatch.Stop();
Console.WriteLine($"Open elapsed time: {stopwatch.ElapsedMilliseconds}");

Console.WriteLine($"""
       Path: {db.Path}
      Table: {db.Table}
    Version: {db.Version}
    """);

stopwatch.Restart();

CRUD.Objects(db);
CRUD.Lists(db);
INDEX.Execute(db);

stopwatch.Stop();
Console.WriteLine($"Operation time: {stopwatch.ElapsedMilliseconds}");

stopwatch.Restart();
db.Dispose();

stopwatch.Stop();
Console.WriteLine($"Close elapsed time: {stopwatch.ElapsedMilliseconds}");
