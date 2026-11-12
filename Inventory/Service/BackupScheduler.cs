using System.Globalization;

namespace Inventory.Service;

/// <summary>
/// Makes one automatic backup per day:
///  - shortly after the app starts, if there is no daily backup for today yet
///    (the counter PC is often off at night, so this is the one that usually runs);
///  - every day at "BackupDailyAt" (default 02:00) while the app is running.
/// Keeps the newest "BackupKeepDaily" daily backups (default 30). Manual backups are never removed.
/// </summary>
public class BackupScheduler : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly BackupService backups;
    private readonly ILogger<BackupScheduler> logger;
    private readonly TimeSpan dailyAt;
    private readonly int keep;

    public BackupScheduler(BackupService backups, IConfiguration configuration, ILogger<BackupScheduler> logger)
    {
        this.backups = backups;
        this.logger = logger;

        dailyAt = TimeSpan.TryParseExact(configuration["BackupDailyAt"], @"hh\:mm", CultureInfo.InvariantCulture, out var at)
            ? at
            : new TimeSpan(2, 0, 0);

        keep = int.TryParse(configuration["BackupKeepDaily"], out var k) && k > 0 ? k : 30;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the app finish starting (and the operator log in) before the first backup.
            await Task.Delay(StartupDelay, stoppingToken);

            if (!backups.HasDailyBackupFor(DateTime.Now))
            {
                await RunBackupAsync();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var next = now.Date + dailyAt;
                if (next <= now)
                {
                    next = next.AddDays(1);
                }

                await Task.Delay(next - now, stoppingToken);

                if (!backups.HasDailyBackupFor(DateTime.Now))
                {
                    await RunBackupAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App is shutting down.
        }
    }

    private async Task RunBackupAsync()
    {
        if (backups.RestartRequired)
        {
            return; // never back up a database the operator has just restored but not restarted with
        }

        try
        {
            var path = await backups.CreateDailyBackupAsync(keep);
            logger.LogInformation("Automatic daily backup created: {Path}", path);
        }
        catch (Exception ex)
        {
            // Never crash the app because of a failed backup; try again at the next scheduled time.
            logger.LogError(ex, "Automatic daily backup failed");
        }
    }
}
