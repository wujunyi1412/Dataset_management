using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class MaterializedDatasetService
{
    private const string OwnershipMarkerFileName = ".dataset-manager-owned.json";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DatasetRecord> CreateAsync(
        string name,
        string sourceManifestPath,
        string destinationParent,
        string imagesFolderName,
        string labelsFolderName,
        string userNotes,
        IProgress<DatasetCopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateFolderName(imagesFolderName, nameof(imagesFolderName));
        ValidateFolderName(labelsFolderName, nameof(labelsFolderName));
        if (imagesFolderName.Equals(labelsFolderName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("图片文件夹和标签文件夹不能同名。");
        if (!File.Exists(sourceManifestPath)) throw new FileNotFoundException("数据集清单不存在。", sourceManifestPath);
        if (!Directory.Exists(destinationParent)) throw new DirectoryNotFoundException($"输出父目录不存在：{destinationParent}");

        CompositeDatasetManifest manifest;
        await using (var stream = File.OpenRead(sourceManifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<CompositeDatasetManifest>(stream, _jsonOptions, cancellationToken)
                ?? throw new InvalidDataException("无法读取数据集清单。");
        }
        if (manifest.Samples.Count == 0) throw new InvalidDataException("数据集清单中没有图片与标签对。");

        var targetRoot = Path.Combine(Path.GetFullPath(destinationParent), SanitizeFileName(name));
        if (Directory.Exists(targetRoot) || File.Exists(targetRoot))
            throw new IOException($"目标数据集目录已经存在：{targetRoot}");

        var imagesPath = Path.Combine(targetRoot, imagesFolderName);
        var labelsPath = Path.Combine(targetRoot, labelsFolderName);
        Directory.CreateDirectory(imagesPath);
        Directory.CreateDirectory(labelsPath);
        try
        {
            var outputSamples = new List<CompositeDatasetSample>(manifest.Samples.Count);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orderedSamples = manifest.Samples.OrderBy(x => x.Index).ToArray();
            for (var index = 0; index < orderedSamples.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sample = orderedSamples[index];
                if (!File.Exists(sample.ImagePath)) throw new FileNotFoundException("来源图片不存在。", sample.ImagePath);
                if (!File.Exists(sample.LabelPath)) throw new FileNotFoundException("来源标签不存在。", sample.LabelPath);

                var outputStem = MakeUniqueStem(Path.GetFileNameWithoutExtension(sample.ImagePath), usedNames);
                var imageDestination = Path.Combine(imagesPath, outputStem + Path.GetExtension(sample.ImagePath).ToLowerInvariant());
                var labelDestination = Path.Combine(labelsPath, outputStem + ".json");
                await CopyFileAsync(sample.ImagePath, imageDestination, cancellationToken);
                await CopyFileAsync(sample.LabelPath, labelDestination, cancellationToken);
                outputSamples.Add(new CompositeDatasetSample
                {
                    Index = index + 1,
                    ImagePath = imageDestination,
                    LabelPath = labelDestination,
                    ProcessedDatasetId = sample.ProcessedDatasetId,
                    AnnotationSetId = sample.AnnotationSetId
                });
                progress?.Report(new DatasetCopyProgress(index + 1, orderedSamples.Length));
            }

            var outputManifest = new CompositeDatasetManifest
            {
                DatasetId = Guid.NewGuid(),
                Name = name,
                Type = DatasetType.Created,
                CreatedAt = DateTimeOffset.Now,
                Notes = $"由清单复制创建：{Path.GetFullPath(sourceManifestPath)}",
                Sources = manifest.Sources,
                Samples = outputSamples
            };
            var outputManifestPath = Path.Combine(targetRoot, "dataset.json");
            await using (var stream = File.Create(outputManifestPath))
                await JsonSerializer.SerializeAsync(stream, outputManifest, _jsonOptions, cancellationToken);
            var markerPath = Path.Combine(targetRoot, OwnershipMarkerFileName);
            await File.WriteAllTextAsync(markerPath, JsonSerializer.Serialize(new OwnershipMarker
            {
                DatasetId = outputManifest.DatasetId,
                CreatedAt = outputManifest.CreatedAt
            }, _jsonOptions), cancellationToken);

            var record = new DatasetRecord
            {
                Id = outputManifest.DatasetId,
                Name = name,
                Type = DatasetType.Created,
                RootPath = targetRoot,
                Notes = outputManifest.Notes + (string.IsNullOrWhiteSpace(userNotes)
                    ? string.Empty
                    : $"{Environment.NewLine}{Environment.NewLine}{userNotes.Trim()}"),
                CreatedAt = outputManifest.CreatedAt,
                UpdatedAt = outputManifest.CreatedAt,
                Materialization = new MaterializedDatasetInfo
                {
                    SourceManifestPath = Path.GetFullPath(sourceManifestPath),
                    ImagesFolderName = imagesFolderName,
                    LabelsFolderName = labelsFolderName,
                    PairCount = outputSamples.Count,
                    OwnsRootDirectory = true
                }
            };
            record.ChangeHistory.Add(new DatasetChangeEntry { Description = $"复制创建数据集，共 {outputSamples.Count} 对文件" });
            return record;
        }
        catch
        {
            if (Directory.Exists(targetRoot)) Directory.Delete(targetRoot, true);
            throw;
        }
    }

    public void DeleteOwnedDataset(DatasetRecord record)
    {
        if (record.Type != DatasetType.Created || record.Materialization?.OwnsRootDirectory != true)
            throw new InvalidOperationException("该记录不是由工具管理的已创建数据集，禁止删除实际目录。");
        var target = Path.GetFullPath(record.RootPath);
        var root = Path.GetPathRoot(target);
        if (string.IsNullOrWhiteSpace(root) || target.TrimEnd(Path.DirectorySeparatorChar).Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("目标路径过于宽泛，拒绝删除。");
        if (Directory.GetParent(target) is null) throw new InvalidOperationException("无法确认目标目录的父目录，拒绝删除。");
        var markerPath = Path.Combine(target, OwnershipMarkerFileName);
        if (!File.Exists(markerPath)) throw new InvalidOperationException("目标目录缺少工具所有权标记，拒绝删除实际文件。");
        OwnershipMarker? marker;
        try
        {
            marker = JsonSerializer.Deserialize<OwnershipMarker>(File.ReadAllText(markerPath), _jsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("工具所有权标记损坏，拒绝删除实际文件。", exception);
        }
        if (marker?.DatasetId != record.Id)
            throw new InvalidOperationException("工具所有权标记与数据集记录不匹配，拒绝删除实际文件。");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string MakeUniqueStem(string? originalStem, HashSet<string> usedNames)
    {
        var stem = string.IsNullOrWhiteSpace(originalStem) ? "sample" : originalStem;
        var candidate = stem;
        var suffix = 2;
        while (!usedNames.Add(candidate)) candidate = $"{stem}_{suffix++}";
        return candidate;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("数据集名称不能为空。", nameof(value));
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(value.Trim().Select(x => invalid.Contains(x) ? '_' : x).ToArray());
        if (result is "." or "..") throw new ArgumentException("数据集名称无效。", nameof(value));
        return result;
    }

    private static void ValidateFolderName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("子文件夹名称必须是单个有效文件夹名。", parameterName);
    }

    private sealed class OwnershipMarker
    {
        public Guid DatasetId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

public readonly record struct DatasetCopyProgress(int Completed, int Total);
