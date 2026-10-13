using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryManagement;

/// <summary>
/// The product / inventory "database": one JSON file on disk, loaded into memory.
/// Also provides backup and recovery (WBS tasks 2 and 24).
/// </summary>
public class Database
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string FilePath { get; }
    public string BackupDirectory { get; }

    /// <summary>Always read the data through this property, because Restore replaces the object.</summary>
    public DataStore Data { get; private set; } = new();

    public Database(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(FilePath) ?? ".";
        BackupDirectory = Path.Combine(directory, "backups");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(BackupDirectory);

        if (File.Exists(FilePath))
            Data = Read(FilePath);
        else
            Save();
    }

    private static DataStore Read(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DataStore>(json, JsonOptions)
                   ?? throw new InventoryException("The data file is empty.");
        }
        catch (JsonException)
        {
            throw new InventoryException($"The file '{path}' is not a valid inventory data file.");
        }
    }

    /// <summary>Writes to a temp file first so a crash can never leave a half-written data file.</summary>
    public void Save()
    {
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Data, JsonOptions));
        File.Move(temp, FilePath, true);
    }

    /// <summary>Copies the data file into the backups folder and returns the new backup's path.</summary>
    public string Backup()
    {
        Save();
        var name = $"inventory_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var destination = Path.Combine(BackupDirectory, name);
        File.Copy(FilePath, destination, true);
        return destination;
    }

    public List<string> ListBackups()
    {
        return Directory.GetFiles(BackupDirectory, "*.json")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Replaces the current data with a backup. The current data is backed up first.</summary>
    public void Restore(string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new InventoryException("Backup file not found.");

        var restored = Read(backupPath); // validate before touching anything
        Backup();                        // safety copy of what we are about to overwrite
        Data = restored;
        Save();
    }
}
