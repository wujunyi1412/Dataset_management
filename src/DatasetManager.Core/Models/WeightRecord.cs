using System.Text.Json.Serialization;

namespace DatasetManager.Core.Models;

public sealed class WeightRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<WeightChangeEntry> ChangeHistory { get; set; } = [];

    [JsonIgnore]
    public string Format => string.IsNullOrWhiteSpace(FilePath)
        ? "未知"
        : Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant() is { Length: > 0 } value ? value : "未知";

    [JsonIgnore]
    public string FileStatus => File.Exists(FilePath) ? "文件存在" : "文件已不存在";
}

public sealed class WeightChangeEntry
{
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.Now;
    public string Description { get; set; } = string.Empty;
}
