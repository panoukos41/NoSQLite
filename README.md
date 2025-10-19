# NoSQLite

[![Release](https://github.com/panoukos41/NoSQLite/actions/workflows/release.yaml/badge.svg)](https://github.com/panoukos41/NoSQLite/actions/workflows/release.yaml)
[![NuGet](https://buildstats.info/nuget/P41.NoSQLite?includePreReleases=true)](https://www.nuget.org/packages/P41.NoSQLite)
[![MIT License](https://img.shields.io/apm/l/atomic-design-ui.svg?)](https://github.com/panoukos41/NoSQLite/blob/main/LICENSE.md)

A C# library to use SQLite as a NoSQL database. This library aims to be simple low level methods that you can use to create your own data access layers.

> [!NOTE]  
> The library is built using [`SQLitePCL.raw`](https://github.com/ericsink/SQLitePCL.raw).

> [!IMPORTANT]  
> To use the library you must ensure you are using an SQLite that contains the [`JSON1`](https://www.sqlite.org/json1.html) extension. The JSON functions and operators are built into SQLite by default, as of SQLite version 3.38.0 (2022-02-22)

## Getting Started

Install some version of [`SQLitePCL.raw`](https://github.com/ericsink/SQLitePCL.raw) to create your `sqlite3` object. 

> [!TIP]  
> You can look at the [test project](./test/NoSQLite.Test/) for more pragmatic usage. The test project uses [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) nuget pacakge to use sqlite.

### Connection

Using your `sqlite3` db create a new instances of the `NoSQLiteConnection` and optionally pass a `JsonSerializerOptions` object.

> [!CAUTION]  
> Once you use the connection be very careful on the consequences of switching your `JsonSerializerOptions` object.

> [!Note]  
> Disposing `NoSQLiteConnection` will not close your `sqlite3` db or do anything with it. It will just cleanup it's associated table and statement instances.

### Tables

You get a table using `connection.GetTable({TableName})`

> [!NOTE]  
> Tables are created if they do not exist. If you request a table multiple times you will always get the same table instance.

At the table level the following methods are supported:
| Method | Description |
|- |- |
| Count/CountLong | Returns the number of rows in the table. |
| All | Returns all rows in the table. Deserialized to `T` |
| Clear | Clears the table. |
| Exists | Check if a document exists. |
| Find | Returns the document if it exists or throws. |
| Add | Adds a document. |
| Update | Updates a document (replace). |
| Delete | Deletes a document. |
| IndexExists | Check if an index exists. |
| CreateIndex | Creates an index if it does not exists using `"{TableName}_{IndexName}"` (can also set unique flag). |
| DeleteIndex | Deletes an index if it does exist. |

### Documents

At the document level the following methods are supported:
| Method | Description |
|- |- |
| FindProperty | Finds a document by key and returns a property value. |
| Insert | Inserts a property value into a document by key. Overwrite `NO`, Create `YES`. |
| Replace | Replaces a property value in a document by key. Overwrite `YES`, Create `NO`. |
| Set | Sets a property value in a document by key. Overwrite `YES`, Create `YES`. |

### Examples

- For connection creation look at the [TestBase Before and After methods of the _setup.cs file](./test/NoSQLite.Test/_setup.cs).
- For CRUD examples look at the [CRUD method of the Table.cs file](./test/NoSQLite.Test/Table.cs).
- For INDEX examples look at the [Index and Index_Unique methods of the Table.cs file](./test/NoSQLite.Test/Table.cs).

## Build

To build this project [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) is needed.

## Contribute

Contributions are welcome and appreciated, before you create a pull request please open a [GitHub Issue](https://github.com/panoukos41/NoSQLite/issues/new) to discuss what needs changing and or fixing if the issue doesn't exist!
