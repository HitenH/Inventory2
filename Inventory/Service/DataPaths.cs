using Microsoft.Data.Sqlite;

namespace Inventory.Service;

/// <summary>
/// Where the database and the backups live.
/// Read from appsettings.json:
///   "DataFolder"   – folder holding Inventory.db. Empty = %ProgramData%\InventoryApp
///   "BackupFolder" – folder holding backups.      Empty = &lt;DataFolder&gt;\Backups
/// Relative paths are resolved against the program folder; %VARIABLES% are expanded.
/// </summary>
public sealed class DataPaths
{
    public const string DatabaseFileName = "Inventory.db";
    public const string MovedNoticeFileName = "Inventory.db-HAS-MOVED.txt";

    public string DataFolder { get; }
    public string BackupFolder { get; }
    public string DatabasePath => Path.Combine(DataFolder, DatabaseFileName);

    /// <summary>The program folder, where the database lived before batch 2.</summary>
    public string LegacyFolder { get; }
    public string LegacyDatabasePath => Path.Combine(LegacyFolder, DatabaseFileName);
    public string LegacyBackupFolder => Path.Combine(LegacyFolder, "Backups");

    private DataPaths(string dataFolder, string backupFolder, string legacyFolder)
    {
        DataFolder = dataFolder;
        BackupFolder = backupFolder;
        LegacyFolder = legacyFolder;
    }

    public static DataPaths FromConfiguration(IConfiguration configuration)
    {
        var programFolder = Path.GetFullPath(AppContext.BaseDirectory);

        var dataFolder = Resolve(configuration["DataFolder"], programFolder)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InventoryApp");

        var backupFolder = Resolve(configuration["BackupFolder"], programFolder)
            ?? Path.Combine(dataFolder, "Backups");

        return new DataPaths(dataFolder, backupFolder, programFolder);
    }

    private static string? Resolve(string? configured, string programFolder)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(configured.Trim());
        return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(programFolder, expanded));
    }

    /// <summary>
    /// One-time move from the program folder to the data folder.
    /// Runs only when the data folder has no database yet and the program folder has one.
    /// The old database is copied (never deleted or changed); a notice file is left next to it.
    /// Old backups are copied too, so they still show on the Settings page.
    /// </summary>
    public void MoveLegacyDatabaseIfNeeded(ILogger logger)
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(BackupFolder);

        if (SamePath(DataFolder, LegacyFolder))
        {
            return; // configured to stay in the program folder (development)
        }

        if (File.Exists(DatabasePath))
        {
            return; // already moved, or a fresh install that has run before
        }

        if (!File.Exists(LegacyDatabasePath))
        {
            logger.LogInformation("No existing database found. A new one will be created at {Path}", DatabasePath);
            return;
        }

        logger.LogWarning("Moving database from {Old} to {New} (copy; the old file is left untouched).", LegacyDatabasePath, DatabasePath);

        // Copy into a temporary file first, so a failure never leaves a half-written database at the new path.
        var temporaryPath = DatabasePath + ".moving";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        // SQLite's own backup API copies the database including changes still in the -wal file.
        SqliteBackup.Copy(LegacyDatabasePath, temporaryPath);
        SqliteBackup.ThrowIfDamaged(temporaryPath);
        File.Move(temporaryPath, DatabasePath);

        var copiedBackups = 0;
        if (Directory.Exists(LegacyBackupFolder) && !SamePath(LegacyBackupFolder, BackupFolder))
        {
            foreach (var file in Directory.GetFiles(LegacyBackupFolder, "*.db"))
            {
                var target = Path.Combine(BackupFolder, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Copy(file, target);
                    copiedBackups++;
                }
            }
        }

        try
        {
            File.WriteAllText(Path.Combine(LegacyFolder, MovedNoticeFileName),
                $"On {DateTime.Now:yyyy-MM-dd HH:mm} the Inventory app copied this database to:{Environment.NewLine}" +
                $"  {DatabasePath}{Environment.NewLine}" +
                $"Backups are now in:{Environment.NewLine}  {BackupFolder}{Environment.NewLine}{Environment.NewLine}" +
                $"The Inventory.db in this folder is no longer used. It was kept as a safety copy.{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write {File}", MovedNoticeFileName);
        }

        logger.LogWarning("Database moved to {New}. {Count} old backup file(s) copied to {Backups}.", DatabasePath, copiedBackups, BackupFolder);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                      Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                      StringComparison.OrdinalIgnoreCase);
}

/// <summary>Low-level SQLite helpers used by backup, restore and the one-time move.</summary>
public static class SqliteBackup
{
    /// <summary>Connection string with pooling off, so the file is released as soon as the connection closes.</summary>
    public static string ConnectionString(string path, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString();

    /// <summary>Copies a whole database using SQLite's online backup (safe while the app is using the source).</summary>
    public static void Copy(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection(ConnectionString(sourcePath, SqliteOpenMode.ReadWrite));
        using var destination = new SqliteConnection(ConnectionString(destinationPath));
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    /// <summary>Runs PRAGMA integrity_check; throws when the file is not a healthy SQLite database.</summary>
    public static void ThrowIfDamaged(string path)
    {
        string? result;
        try
        {
            using var connection = new SqliteConnection(ConnectionString(path, SqliteOpenMode.ReadWrite));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            result = command.ExecuteScalar() as string;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a readable database.", ex);
        }

        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' failed the integrity check: {result}");
        }
    }
}
