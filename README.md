## NoSQLite: NoSQL on top of SQLite

[![Release](https://github.com/panoukos41/NoSQLite/actions/workflows/release.yaml/badge.svg)](https://github.com/panoukos41/NoSQLite/actions/workflows/release.yaml)
[![NuGet](https://buildstats.info/nuget/P41.NoSQLite?includePreReleases=true)](https://www.nuget.org/packages/P41.NoSQLite)
[![MIT License](https://img.shields.io/apm/l/atomic-design-ui.svg?)](https://github.com/panoukos41/NoSQLite/blob/main/LICENSE.md)

A thin wrapper above sqlite using the [`JSON1`](https://www.sqlite.org/json1.html) apis turn it into a [`NOSQL`](https://en.wikipedia.org/wiki/NoSQL) database.

This library references and uses [`SQLitePCLRaw.bundle_e_sqlite3`](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) version `2.1.2` and later witch ensures that the [`JSON1 APIS`](https://www.sqlite.org/json1.html) are present.

The library executes `Batteries.Init();` for you when a connection is first initialized.

## Getting Started

Create an instance of `NoSQLiteConnection`.
```csharp
var connection = new NoSQLiteConnection(
    "path to database file", // Required
    json_options)            // Optional JsonSerializerOptions
```

The connection configures the `PRAGMA journal_mode` to be [`WAL`](https://www.sqlite.org/wal.html)

The connection manages an `sqlite3` object when initialized. *You should always dispose it when you are done so that the databases flashes `WAL` files* but you can also ignore it ¯\ (ツ)/¯.

## Tables

You get a table using `connection.GetTable()` you can optionaly provide a table name or leave it as it is to get the default `documents` table.
> Tables are created if they do not exist.

Tables are managed by their connections so you don't have to worry about disposing. If you request a table multiple times *(eg: the same name)* you will always get the same `Instance`.

## Document Management

Example class that will be used below:
```csharp
public class Person
{
    public string Name { get; set; }
    public string Surname { get; set; }
    public string? Description { get; set; }
}
```
```csharp
var connection = new NoSQLiteConnection("path to database file", "json options or null");
```
```csharp
var docs = connection.GetTable();
```

### Create/Update documents.

Creating or Updating a document happens from the same `Insert` method, keep in mind that this always replaces the document with the new one.
```csharp
var panos = new Person
{
    Name = "panoukos",
    Surname = "41",
    Description = "C# dev"
}

docs.Insert("panos", panos); // If it exists it is now replaced/updated.
```

### Get documents.
```csharp
var doc = docs.Get<Person>("panos"); // Get the document or null.
```

#### Delete documents.
```csharp
docs.Remove("panos"); // Will remove the document.
docs.Remove("panos"); // Will still succeed even if the document doesn't exist.
```

## Build

To build this project [.NET 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) is needed.

## Contribute

Contributions are welcome and appreciated, before you create a pull request please open a [GitHub Issue](https://github.com/panoukos41/NoSQLite/issues/new) to discuss what needs changing and or fixing if the issue doesn't exist!
