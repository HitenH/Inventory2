using Microsoft.Data.Sqlite;

namespace Inventory.Service;

/// <summary>
/// Creates, verifies, lists, restores and deletes database backups.
/// Backups use SQLite's online backup API, so they are consistent even while the app is in use
/// (a plain file copy misses changes still held in the Inventory.db-wal file).
///
/// File names:
///   backup_yyyyMMddHHmmss.db         – made with the Backup button (never deleted automatically)
///   daily_yyyyMMddHHmmss.db          – made by BackupScheduler (only the newest N are kept)
///   before-restore_yyyyMMddHHmmss.db – the live database saved just before a restore
/// </summary>
public class BackupService
{
    public const string ManualPrefix = "backup_";
    public const string DailyPrefix = "daily_";
    public const string BeforeRestorePrefix = "before-restore_";
    private const string TimestampFormat = "yyyyMMddHHmmss";

    private readonly DataPaths paths;
    private readonly ILogger<BackupService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    public BackupService(DataPaths paths, ILogger<BackupService> logger)
    {
        this.paths = paths;
        this.logger = logger;
    }

    public string BackupFolder => paths.BackupFolder;
    public string DatabasePath => paths.DatabasePath;

    /// <summary>True after a restore. The app must be restarted before anyone keeps working.</summary>
    public bool RestartRequired { get; private set; }

    public event Action? RestartRequiredChanged;

    /// <summary>Makes a backup with the Backup button. Returns the new file's path.</summary>
    public Task<string> BackupDatabaseAsync() => CreateAsync(ManualPrefix);

    /// <summary>Makes a scheduled backup and removes daily backups beyond <paramref name="keep"/>.</summary>
    public async Task<string> CreateDailyBackupAsync(int keep)
    {
        var path = await CreateAsync(DailyPrefix);
        PruneDailyBackups(keep);
        return path;
    }

    public bool HasDailyBackupFor(DateTime day) =>
        GetBackupFiles().Any(f => f.Kind == BackupKind.Daily && f.CreatedAt.Date == day.Date);

    private async Task<string> CreateAsync(string prefix)
    {
        await gate.WaitAsync();
        try
        {
            return await Task.Run(() => CreateVerifiedCopy(paths.DatabasePath, prefix));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Copies <paramref name="sourcePath"/> into the backup folder, checks it, and only then gives it its final name.</summary>
    private string CreateVerifiedCopy(string sourcePath, string prefix)
    {
        Directory.CreateDirectory(paths.BackupFolder);

        var finalPath = UniquePath(prefix);
        var temporaryPath = finalPath + ".tmp";

        try
        {
            SqliteBackup.Copy(sourcePath, temporaryPath);
            SqliteBackup.ThrowIfDamaged(temporaryPath);
            File.Move(temporaryPath, finalPath);
            logger.LogInformation("Backup created: {Path}", finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup to {Path} failed", finalPath);
            TryDelete(temporaryPath);
            throw new Exception("Backup failed: " + ex.Message, ex);
        }
    }

    private string UniquePath(string prefix)
    {
        var now = DateTime.Now;
        while (true)
        {
            var path = Path.Combine(paths.BackupFolder, $"{prefix}{now.ToString(TimestampFormat)}.db");
            if (!File.Exists(path))
            {
                return path;
            }

            now = now.AddSeconds(1);
        }
    }

    /// <summary>
    /// Replaces the live data with a backup.
    /// 1. Checks the backup is healthy. 2. Saves the live database as before-restore_*.db.
    /// 3. Writes the backup into the live database with SQLite's backup API (no file overwrite while open).
    /// Afterwards RestartRequired is true.
    /// </summary>
    public async Task RestoreDatabaseAsync(string backupFilePath)
    {
        EnsureInsideBackupFolder(backupFilePath);
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Backup file not found", backupFilePath);
        }

        await gate.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                SqliteBackup.ThrowIfDamaged(backupFilePath);

                var safetyCopy = CreateVerifiedCopy(paths.DatabasePath, BeforeRestorePrefix);
                logger.LogWarning("Restoring {Backup}. Live database saved first as {SafetyCopy}", backupFilePath, safetyCopy);

                SqliteConnection.ClearAllPools();
                using (var source = new SqliteConnection(SqliteBackup.ConnectionString(backupFilePath, SqliteOpenMode.ReadWrite)))
                using (var live = new SqliteConnection(SqliteBackup.ConnectionString(paths.DatabasePath, SqliteOpenMode.ReadWrite)))
                {
                    source.Open();
                    live.Open();
                    source.BackupDatabase(live);
                }
                SqliteConnection.ClearAllPools();

                SqliteBackup.ThrowIfDamaged(paths.DatabasePath);
                logger.LogWarning("Restore of {Backup} finished. The app must be restarted.", backupFilePath);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore of {Backup} failed", backupFilePath);
            throw new Exception("Restore failed: " + ex.Message, ex);
        }
        finally
        {
            gate.Release();
        }

        RestartRequired = true;
        RestartRequiredChanged?.Invoke();
    }

    /// <summary>All backups, newest first.</summary>
    public List<BackupFileInfo> GetBackupFiles()
    {
        if (!Directory.Exists(paths.BackupFolder))
        {
            return new List<BackupFileInfo>();
        }

        return Directory.GetFiles(paths.BackupFolder, "*.db")
            .Select(path => new FileInfo(path))
            .Select(file => new BackupFileInfo(
                file.FullName,
                file.Name,
                file.LastWriteTime,
                file.Length,
                KindOf(file.Name)))
            .OrderByDescending(f => f.CreatedAt)
            .ToList();
    }

    public DateTime? LastBackupTime => GetBackupFiles()
        .Where(f => f.Kind != BackupKind.BeforeRestore)
        .Select(f => (DateTime?)f.CreatedAt)
        .FirstOrDefault();

    public void DeleteBackup(string backupFilePath)
    {
        EnsureInsideBackupFolder(backupFilePath);
        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
            logger.LogInformation("Backup deleted: {Path}", backupFilePath);
        }
    }

    private void PruneDailyBackups(int keep)
    {
        if (keep < 1)
        {
            return;
        }

        foreach (var old in GetBackupFiles().Where(f => f.Kind == BackupKind.Daily).Skip(keep))
        {
            TryDelete(old.Path);
            logger.LogInformation("Old daily backup removed: {Path}", old.Path);
        }
    }

    private static BackupKind KindOf(string fileName) =>
        fileName.StartsWith(DailyPrefix, StringComparison.OrdinalIgnoreCase) ? BackupKind.Daily
        : fileName.StartsWith(BeforeRestorePrefix, StringComparison.OrdinalIgnoreCase) ? BackupKind.BeforeRestore
        : BackupKind.Manual;

    private void EnsureInsideBackupFolder(string filePath)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.BackupFolder));
        var parent = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only files in the backup folder can be restored or deleted.");
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete {Path}", path);
        }
    }
}

public enum BackupKind
{
    Manual,
    Daily,
    BeforeRestore
}

public record BackupFileInfo(string Path, string Name, DateTime CreatedAt, long SizeBytes, BackupKind Kind)
{
    public string KindText => Kind switch
    {
        BackupKind.Daily => "Automatic (daily)",
        BackupKind.BeforeRestore => "Saved before a restore",
        _ => "Manual"
    };

    public string SizeText => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / 1024d / 1024d:0.0} MB"
        : $"{Math.Max(1, SizeBytes / 1024)} KB";
}
