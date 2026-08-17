namespace DatasetManager.Core.Models;

public enum YoloOperationType
{
    FormatConversion,
    DuplicateCheck
}

public sealed class YoloOperationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public YoloOperationType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string SourcePathA { get; set; } = string.Empty;
    public string SourcePathB { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
}
