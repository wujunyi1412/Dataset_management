using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class CompositeDatasetService(ImageLabelPairFinder? pairFinder = null)
{
    private readonly ImageLabelPairFinder _pairFinder = pairFinder ?? new ImageLabelPairFinder();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DatasetRecord> CreateAsync(
        string name,
        DatasetType type,
        string manifestPath,
        string userNotes,
        IReadOnlyList<CompositeSourceRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (type is not (DatasetType.Training or DatasetType.Test or DatasetType.Validation))
            throw new ArgumentException("组合数据集类型必须是训练集、测试集或验证集。", nameof(type));
        if (requests.Count == 0) throw new ArgumentException("至少需要选择一个数据来源。", nameof(requests));

        var record = new DatasetRecord
        {
            Name = name,
            Type = type,
            RootPath = Path.GetFullPath(manifestPath),
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
            Composition = new CompositeDatasetInfo()
        };
        var samples = new List<CompositeDatasetSample>();
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var availablePairs = _pairFinder.Find(request.ProcessedDataset.RootPath, request.AnnotationSet.LabelPath);
            if (request.RequestedCount is not null && request.RequestedCount.Value > availablePairs.Count)
                throw new InvalidOperationException(
                    $"{request.ProcessedDataset.Name} / {request.AnnotationSet.Name} 只有 {availablePairs.Count} 对匹配数据，无法选取 {request.RequestedCount.Value} 对。");
            var takeCount = request.RequestedCount is null
                ? availablePairs.Count
                : request.RequestedCount.Value;
            var sourceInfo = new CompositeSourceInfo
            {
                ProcessedDatasetId = request.ProcessedDataset.Id,
                ProcessedDatasetName = request.ProcessedDataset.Name,
                AnnotationSetId = request.AnnotationSet.Id,
                AnnotationSetName = request.AnnotationSet.Name,
                RequestedCount = request.RequestedCount,
                SelectedCount = takeCount
            };
            record.Composition.Sources.Add(sourceInfo);

            foreach (var pair in availablePairs.Take(takeCount))
            {
                samples.Add(new CompositeDatasetSample
                {
                    Index = samples.Count + 1,
                    ImagePath = pair.ImagePath,
                    LabelPath = pair.LabelPath,
                    ProcessedDatasetId = request.ProcessedDataset.Id,
                    AnnotationSetId = request.AnnotationSet.Id
                });
            }
        }

        record.Composition.PairCount = samples.Count;
        var sourceNotes = string.Join(Environment.NewLine, record.Composition.Sources.Select(x => $"- {x.Summary}"));
        record.Notes = $"组成来源：{Environment.NewLine}{sourceNotes}";
        if (!string.IsNullOrWhiteSpace(userNotes)) record.Notes += $"{Environment.NewLine}{Environment.NewLine}{userNotes.Trim()}";
        record.ChangeHistory.Add(new DatasetChangeEntry { Description = $"创建数据集清单，共 {samples.Count} 对图片与标签" });

        var manifest = new CompositeDatasetManifest
        {
            DatasetId = record.Id,
            Name = record.Name,
            Type = record.Type,
            CreatedAt = record.CreatedAt,
            Notes = record.Notes,
            Sources = record.Composition.Sources,
            Samples = samples
        };
        var directory = Path.GetDirectoryName(record.RootPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = record.RootPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, manifest, _jsonOptions, cancellationToken);
        File.Move(temporaryPath, record.RootPath, true);
        return record;
    }
}
