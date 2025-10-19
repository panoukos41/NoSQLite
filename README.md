# NoSQLite

[![Build Action](https://github.com/panoukos41/NoSQLite/actions/workflows/build.yaml/badge.svg)](https://github.com/panoukos41/NoSQLite/actions/workflows/build.yaml)
[![Publish Action](https://github.com/panoukos41/NoSQLite/actions/workflows/publish.yaml/badge.svg)](https://github.com/panoukos41/NoSQLite/actions/workflows/publish.yaml)
[![Downloads](https://img.shields.io/nuget/dt/P41.NoSQLite.contracts?style=flat)](https://www.nuget.org/packages/P41.NoSQLite/)

[![License](https://img.shields.io/github/license/panoukos41/NoSQLite?style=flat)](./LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET%208-%23512bd4?style=flat)](https://dotnet.microsoft.com)
[![.NET 9](https://img.shields.io/badge/.NET%209-%23512bd4?style=flat)](https://dotnet.microsoft.com)
[![.NET 10](https://img.shields.io/badge/.NET%2010-%23512bd4?style=flat)](https://dotnet.microsoft.com)

A C# library built using [`SQLitePCL.raw`](https://github.com/ericsink/SQLitePCL.raw) to use [SQLite](https://sqlite.org) as a [NoSQL](https://en.wikipedia.org/wiki/NoSQL) database.

The library aims to provide simple low level methods that are used to create your own data access layers. For now the library uses a single `connection` class which creates/uses `tables` that have a single column called `documents`. In reality it should work for other tables with more columns as long as they contain one column called `documents` but no tests have been made or run for this use case.

> [!IMPORTANT]  
> To use the library you must ensure you are using an SQLite that contains the [`JSON1`](https://www.sqlite.org/json1.html) extension. As of version 3.38.0 (2022-02-22) the JSON functions and operators are built into SQLite by default.

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
