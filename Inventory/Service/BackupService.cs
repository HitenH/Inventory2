namespace Inventory.Service;

public class BackupService
{
    private readonly string backupDirectory;

    public BackupService(string backupDirectory)
    {
        this.backupDirectory = backupDirectory;
    }

    public void BackupDatabase()
    {
        try
        {
            //Get source directory and copy it to backup directory
            //Inventory.db is always there in the source directory
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            string backupPath = Path.Combine(backupDirectory, $"backup_{DateTime.Now:yyyyMMddHHmmss}.db");
            File.Copy("/Inventory.db", backupPath, true);
        }
        catch (Exception ex)
        {
            throw new Exception("Backup failed", ex);
        }
    }

    public void RestoreDatabase(string backupFilePath)
    {
        try
        {
            //Get source directory and copy it to backup directory
            //Inventory.db is always there in the source directory
            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException("Backup file not found", backupFilePath);
            }
            File.Copy(backupFilePath, "/Inventory.db", true);
        }
        catch (Exception ex)
        {
            throw new Exception("Restore failed", ex);
        }
    }

    //Method to list all the backup files
    public List<string> GetBackupFiles()
    {
        //First check if path exists, if not return empty list
        if (!Directory.Exists(backupDirectory))
        {
            return new List<string>();
        }

        //Get all the files in the directory
        return Directory.GetFiles(backupDirectory).ToList();
    }

    //Delete a backup
    public void DeleteBackup(string backupFilePath)
    {
        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
        }
    }
}
