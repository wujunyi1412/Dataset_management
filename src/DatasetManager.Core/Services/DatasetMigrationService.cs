using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class DatasetMigrationService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DatasetMigrationResult> PrepareAsync(
        DatasetRecord record,
        string destinationRoot,
        IProgress<DatasetMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (record.Type is not (DatasetType.Raw or DatasetType.Processed or DatasetType.Created))
            throw new InvalidOperationException("只有原始数据集、已处理数据集和已创建数据集支持迁移。");

        var sourceRoot = NormalizeDirectory(record.RootPath, "数据集原路径不存在");
        var targetRoot = Path.GetFullPath(destinationRoot.Trim());
        ValidateDestination(sourceRoot, targetRoot);

        var annotationMappings = BuildAnnotationMappings(record, sourceRoot, targetRoot);
        var externalMappings = annotationMappings
            .Where(x => !IsSameOrDescendant(x.SourcePath, sourceRoot))
            .GroupBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
        ValidateExternalSources(sourceRoot, externalMappings);

        var sources = new[] { new DirectoryMapping(sourceRoot, targetRoot) }
            .Concat(externalMappings.Select(x => new DirectoryMapping(x.SourcePath, x.DestinationPath)))
            .ToArray();
        var totalFiles = await Task.Run(
            () => sources.Sum(x => CountFiles(x.SourcePath)),
            cancellationToken);
        var copiedFiles = 0;

        try
        {
            foreach (var mapping in sources)
            {
                await CopyDirectoryAsync(mapping.SourcePath, mapping.DestinationPath, () =>
                {
                    copiedFiles++;
                    progress?.Report(new DatasetMigrationProgress(copiedFiles, totalFiles));
                }, cancellationToken);
            }

            if (record.Type == DatasetType.Created)
                await RewriteCreatedManifestAsync(sourceRoot, targetRoot, cancellationToken);

            return new DatasetMigrationResult(
                sourceRoot,
                targetRoot,
                annotationMappings.ToDictionary(x => x.AnnotationId, x => x.DestinationPath),
                externalMappings.Select(x => x.SourcePath).ToArray(),
                copiedFiles);
        }
        catch
        {
            TryDeleteDirectory(targetRoot);
            throw;
        }
    }

    public IReadOnlyList<string> DeleteSources(DatasetMigrationResult result)
    {
        var failures = new List<string>();
        var paths = result.ExternalSourcePaths
            .Append(result.SourceRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length);
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                failures.Add($"{path}（{exception.Message}）");
            }
        }
        return failures;
    }

    public void DeletePreparedDestination(DatasetMigrationResult result) => TryDeleteDirectory(result.DestinationRoot);

    private List<AnnotationMapping> BuildAnnotationMappings(DatasetRecord record, string sourceRoot, string targetRoot)
    {
        var result = new List<AnnotationMapping>();
        var externalDestinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reservedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var annotationsContainer = MakeUniqueChildPath(targetRoot, "annotations", sourceRoot, reservedDestinations);

        foreach (var annotation in record.AnnotationSets ?? [])
        {
            var source = NormalizeDirectory(annotation.LabelPath, $"标注批次“{annotation.Name}”的路径不存在");
            string destination;
            if (IsSameOrDescendant(source, sourceRoot))
            {
                destination = Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, source));
            }
            else if (!externalDestinations.TryGetValue(source, out destination!))
            {
                destination = MakeUniqueChildPath(
                    annotationsContainer,
                    SanitizeName(annotation.Name, "labels"),
                    null,
                    reservedDestinations);
                externalDestinations[source] = destination;
            }
            result.Add(new AnnotationMapping(annotation.Id, source, destination));
        }
        return result;
    }

    private static void ValidateDestination(string sourceRoot, string targetRoot)
    {
        if (string.IsNullOrWhiteSpace(targetRoot)) throw new ArgumentException("目标路径不能为空。", nameof(targetRoot));
        if (Directory.Exists(targetRoot) || File.Exists(targetRoot))
            throw new IOException($"目标路径已经存在，为避免覆盖，迁移已停止：{targetRoot}");
        if (IsSameOrDescendant(targetRoot, sourceRoot) || IsSameOrDescendant(sourceRoot, targetRoot))
            throw new InvalidOperationException("新路径不能与原路径相同，也不能互为父子目录。");
        var parent = Directory.GetParent(targetRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            throw new DirectoryNotFoundException($"目标路径的父目录不存在：{parent ?? targetRoot}");
    }

    private static void ValidateExternalSources(string sourceRoot, IReadOnlyList<AnnotationMapping> mappings)
    {
        for (var i = 0; i < mappings.Count; i++)
        {
            if (IsSameOrDescendant(sourceRoot, mappings[i].SourcePath))
                throw new InvalidOperationException($"外置标签目录包含数据集主目录，无法安全迁移：{mappings[i].SourcePath}");
            for (var j = i + 1; j < mappings.Count; j++)
            {
                if (IsSameOrDescendant(mappings[i].SourcePath, mappings[j].SourcePath)
                    || IsSameOrDescendant(mappings[j].SourcePath, mappings[i].SourcePath))
                    throw new InvalidOperationException("多个外置标签目录互相嵌套，无法安全迁移。请先整理这些标签路径。");
            }
        }
    }

    private async Task RewriteCreatedManifestAsync(string sourceRoot, string targetRoot, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(targetRoot, "dataset.json");
        if (!File.Exists(manifestPath)) return;
        CompositeDatasetManifest manifest;
        await using (var input = File.OpenRead(manifestPath))
            manifest = await JsonSerializer.DeserializeAsync<CompositeDatasetManifest>(input, _jsonOptions, cancellationToken)
                ?? throw new InvalidDataException("已创建数据集的 dataset.json 无法读取。");

        foreach (var sample in manifest.Samples)
        {
            sample.ImagePath = RemapPath(sample.ImagePath, sourceRoot, targetRoot);
            sample.LabelPath = RemapPath(sample.LabelPath, sourceRoot, targetRoot);
        }
        var temporaryPath = manifestPath + ".tmp";
        await using (var output = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(output, manifest, _jsonOptions, cancellationToken);
        File.Move(temporaryPath, manifestPath, true);
    }

    private static string RemapPath(string path, string sourceRoot, string targetRoot)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        var fullPath = Path.GetFullPath(path);
        return IsSameOrDescendant(fullPath, sourceRoot)
            ? Path.GetFullPath(Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, fullPath)))
            : fullPath;
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        Action fileCopied,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outputPath = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            await input.CopyToAsync(output, cancellationToken);
            File.SetLastWriteTimeUtc(outputPath, File.GetLastWriteTimeUtc(file));
            fileCopied();
        }
    }

    private static int CountFiles(string path) => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();

    private static string NormalizeDirectory(string path, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException($"{errorMessage}：{path}");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsSameOrDescendant(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), Path.GetFullPath(candidate));
        return relative == "." || (!relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative));
    }

    private static string MakeUniqueChildPath(string parent, string name, string? sourceRoot, HashSet<string> reserved)
    {
        var candidate = Path.Combine(parent, name);
        var suffix = 2;
        while (reserved.Contains(candidate)
               || (sourceRoot is not null && Directory.Exists(Path.Combine(sourceRoot, Path.GetRelativePath(parent, candidate)))))
            candidate = Path.Combine(parent, $"{name}_{suffix++}");
        reserved.Add(candidate);
        return candidate;
    }

    private static string SanitizeName(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string((value ?? string.Empty).Trim().Select(x => invalid.Contains(x) ? '_' : x).ToArray());
        return string.IsNullOrWhiteSpace(result) || result is "." or ".." ? fallback : result;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { /* 保留原始异常；残留目标目录可由用户确认后处理。 */ }
    }

    private sealed record AnnotationMapping(Guid AnnotationId, string SourcePath, string DestinationPath);
    private sealed record DirectoryMapping(string SourcePath, string DestinationPath);
}

public sealed record DatasetMigrationResult(
    string SourceRoot,
    string DestinationRoot,
    IReadOnlyDictionary<Guid, string> AnnotationPaths,
    IReadOnlyList<string> ExternalSourcePaths,
    int CopiedFileCount)
{
    public string RemapRootedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(SourceRoot, fullPath);
        return relative == "." || (!Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            ? Path.GetFullPath(Path.Combine(DestinationRoot, relative))
            : fullPath;
    }
}

public readonly record struct DatasetMigrationProgress(int CompletedFiles, int TotalFiles);

public enum DatasetMigrationMode
{
    Copy,
    Move
}
