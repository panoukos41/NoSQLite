using System.Diagnostics.CodeAnalysis;

namespace NoSQLite;

/// <summary>
/// Provides extension methods for the <c>sqlite3</c> type to simplify database creation and disposal.
/// </summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Matching the lib name.")]
public static class sqlite3_mixins
{
    extension(sqlite3)
    {
        /// <summary>
        /// Creates or opens an SQLite database at the specified path.
        /// Optionally enables Write-Ahead Logging (WAL) mode.
        /// </summary>
        /// <param name="databasePath">The file path to the SQLite database.</param>
        /// <param name="useWal">If <see langword="true"/>, enables WAL journal mode. Default is <see langword="true"/>.</param>
        /// <returns>An instance of <see cref="sqlite3"/> representing the opened database.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="databasePath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="NoSQLiteException">Thrown if the database could not be opened.</exception>
        public static sqlite3 Create(string databasePath, bool useWal = true)
        {
            ArgumentException.ThrowIfNullOrEmpty(databasePath);

            var result = sqlite3_open(databasePath, out var db);
            db.CheckResult(result, $"Could not open or create database file: {databasePath}");

            if (useWal)
            {
                sqlite3_exec(db, "PRAGMA journal_mode=WAL;");
            }
            return db;
        }
    }

    extension(sqlite3 db)
    {
        /// <summary>
        /// Executes a write-ahead log (WAL) checkpoint for the database.
        /// </summary>
        /// <remarks>See <see href="https://sqlite.org/c3ref/wal_checkpoint.html"/> for more info.</remarks>
        public void Checkpoint(string name)
        {
            sqlite3_wal_checkpoint(db, name);
        }

        /// <summary>
        /// Executes a write-ahead log (WAL) checkpoint for the database.
        /// </summary>
        /// <remarks>See <see href="https://sqlite.org/c3ref/wal_checkpoint_v2.html"/> for more info.</remarks>
        public (int LogSize, int FramesCheckPointed) CheckpointV2(string name, int eMode)
        {
            sqlite3_wal_checkpoint_v2(db, name, eMode, out var logSize, out var framesCheckPointed);
            return (logSize, framesCheckPointed);
        }

        /// <summary>
        /// Closes and disposes the specified <c>sqlite3</c> database connection.
        /// </summary>
        /// <param name="db">The <c>sqlite3</c> database connection to dispose.</param>
        public void CloseAndDispose()
        {
            sqlite3_close_v2(db);
            db.Dispose();
        }
    }
}
