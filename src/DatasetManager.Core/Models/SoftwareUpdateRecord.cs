using System.Text.Json.Serialization;

namespace DatasetManager.Core.Models;

public sealed class SoftwareUpdateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastScannedAt { get; set; }
    public Dictionary<string, SoftwareFileSnapshot> Snapshot { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<SoftwareUpdateScan> ScanHistory { get; set; } = [];

    [JsonIgnore]
    public string FolderStatus => Directory.Exists(FolderPath) ? "文件夹存在" : "文件夹已不存在";

    [JsonIgnore]
    public string LastScanSummary => LastScannedAt is null
        ? "尚未扫描"
        : $"上次扫描：{LastScannedAt:yyyy-MM-dd HH:mm} · {Snapshot.Count:N0} 个文件";
}

public sealed class SoftwareFileSnapshot
{
    public long Length { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}

public sealed class SoftwareUpdateScan
{
    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.Now;
    public bool IsBaseline { get; set; }
    public int TotalFileCount { get; set; }
    public List<string> AddedFiles { get; set; } = [];
    public List<string> ModifiedFiles { get; set; } = [];
    public List<string> DeletedFiles { get; set; } = [];

    [JsonIgnore]
    public int ChangeCount => AddedFiles.Count + ModifiedFiles.Count + DeletedFiles.Count;

    [JsonIgnore]
    public string Summary => IsBaseline
        ? $"建立初始基线，共 {TotalFileCount:N0} 个文件"
        : ChangeCount == 0
            ? $"未发现变化，共 {TotalFileCount:N0} 个文件"
            : $"新增 {AddedFiles.Count:N0} · 修改 {ModifiedFiles.Count:N0} · 删除 {DeletedFiles.Count:N0}";
}
