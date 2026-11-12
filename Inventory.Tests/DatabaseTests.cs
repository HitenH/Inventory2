using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.Tests;

/// <summary>Database layout and backups.</summary>
public class DatabaseTests
{
    private static BackupService Backups(TestDatabase db)
    {
        var paths = DataPaths.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataFolder"] = db.Folder,
            ["BackupFolder"] = Path.Combine(db.Folder, "Backups"),
        }).Build());
        Directory.CreateDirectory(paths.BackupFolder);
        return new BackupService(paths, NullLogger<BackupService>.Instance);
    }

    [Fact]
    public void Migrations_match_the_code()
    {
        // If this fails, a table was changed in code without adding a migration.
        using var db = new TestDatabase();
        using var context = db.Open();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(context.Database.GetPendingMigrations());
    }

    [Fact]
    public async Task Backup_contains_the_latest_data_and_restore_brings_it_back()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var backups = Backups(db);

        var backupFile = await backups.BackupDatabaseAsync();
        Assert.True(File.Exists(backupFile));
        Assert.Single(backups.GetBackupFiles(), f => f.Kind == BackupKind.Manual);

        // A change made after the backup...
        await using (var c = db.Open())
        {
            c.Products.Add(new ProductEntity { ProductId = "P-after", Name = "Added after backup" });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // ...is gone after restoring it.
        SqliteConnection.ClearAllPools();
        await backups.RestoreDatabaseAsync(backupFile);
        Assert.True(backups.RestartRequired);
        SqliteConnection.ClearAllPools();
        await using (var c = db.Open())
        {
            Assert.False(c.Products.Any(p => p.ProductId == "P-after"));
            Assert.True(c.Products.Any(p => p.ProductId == "P1"));
        }

        // A safety copy of the database is made before every restore.
        Assert.Contains(backups.GetBackupFiles(), f => f.Name.StartsWith(BackupService.BeforeRestorePrefix));
    }

    [Fact]
    public async Task A_damaged_backup_is_refused_and_the_database_is_left_alone()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var backups = Backups(db);
        var damaged = Path.Combine(backups.BackupFolder, "backup_damaged.db");
        await File.WriteAllTextAsync(damaged, "this is not a database", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() => backups.RestoreDatabaseAsync(damaged));
        Assert.False(backups.RestartRequired);
        await using var c = db.Open();
        Assert.True(c.Products.Any(p => p.ProductId == "P1"));
    }
}
