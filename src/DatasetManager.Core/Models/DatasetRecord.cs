using System.Text.Json.Serialization;

namespace DatasetManager.Core.Models;

public sealed class DatasetRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DatasetType Type { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public Guid? ParentDatasetId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<DatasetChangeEntry> ChangeHistory { get; set; } = [];
    public List<AnnotationSetRecord> AnnotationSets { get; set; } = [];
    public CompositeDatasetInfo? Composition { get; set; }
    public MaterializedDatasetInfo? Materialization { get; set; }
    public DatasetStatistics Statistics { get; set; } = new();
}

public sealed class MaterializedDatasetInfo
{
    public string SourceManifestPath { get; set; } = string.Empty;
    public string ImagesFolderName { get; set; } = "images";
    public string LabelsFolderName { get; set; } = "jsons";
    public int PairCount { get; set; }
    public bool OwnsRootDirectory { get; set; }
}

public sealed class CompositeDatasetInfo
{
    public int PairCount { get; set; }
    public List<CompositeSourceInfo> Sources { get; set; } = [];
}

public sealed class CompositeSourceInfo
{
    public Guid ProcessedDatasetId { get; set; }
    public string ProcessedDatasetName { get; set; } = string.Empty;
    public Guid AnnotationSetId { get; set; }
    public string AnnotationSetName { get; set; } = string.Empty;
    public int? RequestedCount { get; set; }
    public int SelectedCount { get; set; }

    [JsonIgnore]
    public string Summary => RequestedCount is null
        ? $"{ProcessedDatasetName} / {AnnotationSetName}：全部，共 {SelectedCount} 对"
        : $"{ProcessedDatasetName} / {AnnotationSetName}：{SelectedCount} 对";
}

public sealed class DatasetChangeEntry
{
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.Now;
    public string Description { get; set; } = string.Empty;
}

public sealed class DatasetStatistics
{
    public int ImageCount { get; set; }
    public int SubfolderCount { get; set; }
    public List<FolderImageStatistics> ImageFolders { get; set; } = [];
    public List<string> Resolutions { get; set; } = [];
    public List<int> BitDepths { get; set; } = [];
    public List<int> ChannelCounts { get; set; } = [];
    public int UnreadableImageCount { get; set; }
    public DateTimeOffset? ScannedAt { get; set; }
    public string? ScanError { get; set; }
}

public sealed class AnnotationSetRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string LabelPath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public AnnotationStatistics Statistics { get; set; } = new();
}

public sealed class AnnotationStatistics
{
    public int JsonFileCount { get; set; }
    public int MatchedFileCount { get; set; }
    public int UnmatchedLabelFileCount { get; set; }
    public int UnmatchedImageFileCount { get; set; }
    public int AnnotationCount { get; set; }
    public int InvalidFileCount { get; set; }
    public Dictionary<string, int> ClassCounts { get; set; } = new(StringComparer.Ordinal);
    public DateTimeOffset? ScannedAt { get; set; }
    public DateTimeOffset? MatchingScannedAt { get; set; }
    public string? ScanError { get; set; }

    [JsonIgnore]
    public string MatchingSummary => MatchingScannedAt is null
        ? "尚未统计图片与标签匹配，请点击“统计标签与匹配”"
        : $"匹配成功 {MatchedFileCount} 对　无图片标签 {UnmatchedLabelFileCount} 个　无标签图片 {UnmatchedImageFileCount} 张";
}

public sealed class FolderImageStatistics
{
    public string RelativePath { get; set; } = string.Empty;
    public int ImageCount { get; set; }
    public Dictionary<string, int> ExtensionCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string DisplayPath => string.IsNullOrEmpty(RelativePath) ? "根目录" : RelativePath;

    [JsonIgnore]
    public string FormatSummary => string.Join("，", ExtensionCounts
        .OrderBy(x => x.Key)
        .Select(x => $"{x.Key.TrimStart('.').ToUpperInvariant()} {x.Value} 张"));
}
