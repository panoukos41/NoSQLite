## NoSQLite: NoSQL on top of SQLite

[![Release](https://github.com/panoukos41/NoSQLite/actions/workflows/release.yaml/badge.svg)](https://github.com/panoukos41/NoSQLite/actions/workflows/release.yaml)
[![NuGet](https://buildstats.info/nuget/P41.NoSQLite?includePreReleases=true)](https://www.nuget.org/packages/P41.NoSQLite)
[![MIT License](https://img.shields.io/apm/l/atomic-design-ui.svg?)](https://github.com/panoukos41/NoSQLite/blob/main/LICENSE.md)

A thin wrapper above sqlite to use it as a nosql database.

## Getting Started

All you need to is an instance of `NoSQLiteConnection`, the instance requires a `string` that is a full path to the database file and you can optionally provide your own `JsonSerializerOptions` to configure the JSON serialization/deserialization.

`NoSQLiteConnection` create and open a connection when initialized so keep in mind that you will have to dispose it when you are done using the `Dispose` method of the `IDisposable` `interface`.

In most scenarios you will create it and store it to use it until you should down your application.

The `sqlite` database is configured with `journal_mode` set to `WAL`.

### How It Works

It works by taking advantage of the [JSON1](https://www.sqlite.org/json1.html) through the [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) nuget package that forces the correct version of sqlite to manipulate the documents, create indexes and more in the future!

### Document Management

Example class that will be used below:
```csharp
public class Person
{
    public string Name { get; set; }
    public string Surname { get; set; }
    public string? Description { get; set; }
}
```

and the same connection:
```csharp
var db = new NoSQLiteConnection("path to database file", "json options or null");
```

#### Creating/Updating documents.

Creating or Updating a document happens from the same `insert` method, keep in mind that this always replaces the document with the new one.
```csharp
var panos = new Person
{
    Name = "panoukos",
    Surname = "41",
    Description = "C# dev"
}

db.Insert("panos", panos);
```

### Get documents.
```csharp
var doc = db.Get<Person>("panos");
```

#### Deleting documents.
```csharp
db.Remove("panos"); // Will remove the document.
db.Remove("panos"); // Will still succeed even if the document doesn't exist.
```

## Build

To build this project [.NET 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) is needed.

## Contribute

Contributions are welcome and appreciated, before you create a pull request please open a [GitHub Issue](https://github.com/panoukos41/NoSQLite/issues/new) to discuss what needs changing and or fixing if the issue doesn't exist!
