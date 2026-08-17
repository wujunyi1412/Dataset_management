namespace DatasetManager.Core.Services;

public static class ApplicationDataPaths
{
    private static readonly object InitializationLock = new();
    private static bool _initialized;

    public static string RecordDirectory => Path.Combine(AppContext.BaseDirectory, "records");
    public static string LogDirectory => Path.Combine(RecordDirectory, "logs");

    public static string GetRecordFile(string fileName)
    {
        EnsureInitialized();
        return Path.Combine(RecordDirectory, fileName);
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (InitializationLock)
        {
            if (_initialized) return;
            Directory.CreateDirectory(RecordDirectory);
            var legacyDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DatasetManager");
            foreach (var fileName in new[] { "catalog.json", "yolo-operations.json", "weights.json" })
                CopyIfMissing(Path.Combine(legacyDirectory, fileName), Path.Combine(RecordDirectory, fileName));

            var legacyLogs = Path.Combine(legacyDirectory, "logs");
            if (Directory.Exists(legacyLogs))
            {
                Directory.CreateDirectory(LogDirectory);
                foreach (var sourcePath in Directory.EnumerateFiles(legacyLogs, "*", SearchOption.TopDirectoryOnly))
                    CopyIfMissing(sourcePath, Path.Combine(LogDirectory, Path.GetFileName(sourcePath)));
            }
            _initialized = true;
        }
    }

    private static void CopyIfMissing(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(destinationPath)) return;
        File.Copy(sourcePath, destinationPath, false);
    }
}
