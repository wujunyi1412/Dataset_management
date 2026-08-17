namespace DatasetManager.Core.Models;

public sealed class CompositeDatasetManifest
{
    public int Version { get; set; } = 1;
    public Guid DatasetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatasetType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<CompositeSourceInfo> Sources { get; set; } = [];
    public List<CompositeDatasetSample> Samples { get; set; } = [];
}

public sealed class CompositeDatasetSample
{
    public int Index { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string LabelPath { get; set; } = string.Empty;
    public Guid ProcessedDatasetId { get; set; }
    public Guid AnnotationSetId { get; set; }
}

public sealed record CompositeSourceRequest(
    DatasetRecord ProcessedDataset,
    AnnotationSetRecord AnnotationSet,
    int? RequestedCount);
