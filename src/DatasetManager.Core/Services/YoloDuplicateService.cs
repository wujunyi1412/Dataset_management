namespace DatasetManager.Core.Services;

public enum DuplicateDeleteTarget
{
    None,
    FolderA,
    FolderB
}

public sealed class YoloDuplicateService
{
    public YoloDuplicateResult Check(string folderA, string folderB)
    {
        if (!Directory.Exists(folderA)) throw new DirectoryNotFoundException($"目录 A 不存在：{folderA}");
        if (!Directory.Exists(folderB)) throw new DirectoryNotFoundException($"目录 B 不存在：{folderB}");
        if (Path.GetFullPath(folderA).Equals(Path.GetFullPath(folderB), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("目录 A 和目录 B 不能是同一个目录。");

        var filesA = EnumerateTxt(folderA);
        var filesB = EnumerateTxt(folderB);
        var duplicateNames = filesA.Keys.Intersect(filesB.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new YoloDuplicateResult(folderA, folderB, filesA, filesB, duplicateNames);
    }

    public int DeleteDuplicates(YoloDuplicateResult result, DuplicateDeleteTarget target)
    {
        if (target == DuplicateDeleteTarget.None) return 0;
        var files = target == DuplicateDeleteTarget.FolderA ? result.FilesA : result.FilesB;
        var deleted = 0;
        foreach (var name in result.DuplicateNames)
        {
            if (!files.TryGetValue(name, out var paths)) continue;
            foreach (var path in paths)
            {
                if (!File.Exists(path) || !Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)) continue;
                File.Delete(path);
                deleted++;
            }
        }
        return deleted;
    }

    private static Dictionary<string, string[]> EnumerateTxt(string root) =>
        Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories)
            .Where(x => !Path.GetFileName(x).Equals("classes.txt", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => Path.GetFileName(x) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
}

public sealed record YoloDuplicateResult(
    string FolderA,
    string FolderB,
    IReadOnlyDictionary<string, string[]> FilesA,
    IReadOnlyDictionary<string, string[]> FilesB,
    IReadOnlyList<string> DuplicateNames)
{
    public int DuplicateNameCount => DuplicateNames.Count;
    public int FileCountA => FilesA.Values.Sum(x => x.Length);
    public int FileCountB => FilesB.Values.Sum(x => x.Length);
}
