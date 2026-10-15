using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class SoftwareUpdateScanner
{
    public Task<SoftwareUpdateScanResult> ScanAsync(
        string folderPath,
        IReadOnlyDictionary<string, SoftwareFileSnapshot> previousSnapshot,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(folderPath, previousSnapshot, cancellationToken), cancellationToken);

    private static SoftwareUpdateScanResult Scan(
        string folderPath,
        IReadOnlyDictionary<string, SoftwareFileSnapshot> previousSnapshot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"文件夹不存在：{folderPath}");

        var current = new Dictionary<string, SoftwareFileSnapshot>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(folderPath));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            foreach (var filePath in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(filePath);
                current[Path.GetRelativePath(folderPath, filePath)] = new SoftwareFileSnapshot
                {
                    Length = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc
                };
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    pending.Push(child);
        }

        var added = current.Keys.Except(previousSnapshot.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var deleted = previousSnapshot.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var modified = current.Keys.Intersect(previousSnapshot.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(path => current[path].Length != previousSnapshot[path].Length
                           || current[path].LastWriteTimeUtc != previousSnapshot[path].LastWriteTimeUtc)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        return new SoftwareUpdateScanResult(current, added, modified, deleted);
    }
}

public sealed record SoftwareUpdateScanResult(
    Dictionary<string, SoftwareFileSnapshot> Snapshot,
    List<string> AddedFiles,
    List<string> ModifiedFiles,
    List<string> DeletedFiles);
