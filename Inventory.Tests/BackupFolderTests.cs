using Inventory.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.Tests;

/// <summary>Choosing the backup folder on the Settings page.</summary>
public class BackupFolderTests
{
    private static DataPaths Paths(TestDatabase db) =>
        DataPaths.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataFolder"] = db.Folder,
            ["BackupFolder"] = Path.Combine(db.Folder, "Backups"),
        }).Build());

    private static BackupService Backups(DataPaths paths)
    {
        Directory.CreateDirectory(paths.BackupFolder);
        return new BackupService(paths, NullLogger<BackupService>.Instance);
    }

    [Fact]
    public async Task A_chosen_folder_is_used_for_new_backups_and_remembered_after_a_restart()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var backups = Backups(Paths(db));
        var chosen = Path.Combine(db.Folder, "USB", "Inventory Backups");

        await backups.ChangeBackupFolderAsync(chosen, copyExisting: false);

        Assert.Equal(chosen, backups.BackupFolder);
        var file = await backups.BackupDatabaseAsync();
        Assert.Equal(chosen, Path.GetDirectoryName(file));

        // A fresh start (new DataPaths) reads the choice back, even though appsettings says otherwise.
        Assert.Equal(chosen, Paths(db).BackupFolder);
    }

    [Fact]
    public async Task Existing_backups_are_copied_only_when_asked_and_the_originals_stay()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var backups = Backups(Paths(db));
        var original = await backups.BackupDatabaseAsync();

        var withCopy = Path.Combine(db.Folder, "withCopy");
        Assert.Equal(1, await backups.ChangeBackupFolderAsync(withCopy, copyExisting: true));
        Assert.Single(backups.GetBackupFiles());
        Assert.True(File.Exists(original));

        var withoutCopy = Path.Combine(db.Folder, "withoutCopy");
        Assert.Equal(0, await backups.ChangeBackupFolderAsync(withoutCopy, copyExisting: false));
        Assert.Empty(backups.GetBackupFiles());
    }

    [Fact]
    public async Task Choosing_the_standard_folder_forgets_the_choice()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var paths = Paths(db);
        var backups = Backups(paths);

        await backups.ChangeBackupFolderAsync(Path.Combine(db.Folder, "Elsewhere"), copyExisting: false);
        Assert.True(File.Exists(paths.ChosenBackupFolderFile));

        await backups.ChangeBackupFolderAsync(paths.StandardBackupFolder, copyExisting: false);
        Assert.False(File.Exists(paths.ChosenBackupFolderFile));
        Assert.Equal(paths.StandardBackupFolder, Paths(db).BackupFolder);
    }

    [Fact]
    public async Task The_database_folder_and_relative_paths_are_refused()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var backups = Backups(Paths(db));
        var before = backups.BackupFolder;

        // Would list Inventory.db itself as a backup that could be restored over or deleted.
        Assert.NotNull(backups.CheckNewBackupFolder(db.Folder));
        await Assert.ThrowsAsync<InvalidOperationException>(() => backups.ChangeBackupFolderAsync(db.Folder, false));

        Assert.NotNull(backups.CheckNewBackupFolder("Backups2"));
        Assert.NotNull(backups.CheckNewBackupFolder(""));
        Assert.NotNull(backups.CheckNewBackupFolder(before));
        Assert.Equal(before, backups.BackupFolder);
    }

    [Fact]
    public async Task An_unreachable_backup_folder_does_not_stop_the_app_starting()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var paths = Paths(db);
        // Pretend the USB drive is unplugged: point the saved choice at a drive letter that does not exist.
        var missingDrive = Enumerable.Range('D', 23).Select(c => $"{(char)c}:\\")
            .First(root => !Directory.Exists(root));
        await File.WriteAllTextAsync(paths.ChosenBackupFolderFile, Path.Combine(missingDrive, "Backups"), TestContext.Current.CancellationToken);

        var restarted = Paths(db);
        restarted.MoveLegacyDatabaseIfNeeded(NullLogger.Instance); // must not throw
        var backups = new BackupService(restarted, NullLogger<BackupService>.Instance);
        Assert.False(backups.BackupFolderReachable);
        Assert.Empty(backups.GetBackupFiles());
        await Assert.ThrowsAnyAsync<Exception>(() => backups.BackupDatabaseAsync());
    }
}
