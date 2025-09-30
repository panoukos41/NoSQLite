global using ConsoleApp.Data;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using ConsoleApp;
using NoSQLite;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;


//BenchmarkRunner.Run<Benchy>();

//return;

var stopwatch = Stopwatch.StartNew();

var connection = new NoSQLiteConnection(
    Path.Combine(Environment.CurrentDirectory, "console.sqlite3"),
    new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });

stopwatch.Stop();
Console.WriteLine($"Open elapsed time: {stopwatch.ElapsedMilliseconds}");

Console.WriteLine($"""
       Path: {connection.Path}
    Version: {connection.Version}
    """);

stopwatch.Restart();

CRUD.Objects(connection);
CRUD.Lists(connection);
INDEX.Execute(connection);

stopwatch.Stop();
Console.WriteLine($"Operation time: {stopwatch.ElapsedMilliseconds}");

stopwatch.Restart();
connection.Dispose();

stopwatch.Stop();
Console.WriteLine($"Close elapsed time: {stopwatch.ElapsedMilliseconds}");
