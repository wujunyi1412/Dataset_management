using System.Text.Json;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class LabelMeAnnotationScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"
    };

    public Task<AnnotationStatistics> ScanAsync(
        string labelPath,
        string imagePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(labelPath, imagePath, cancellationToken), cancellationToken);

    private static AnnotationStatistics Scan(string labelPath, string imagePath, CancellationToken cancellationToken)
    {
        var result = new AnnotationStatistics { ScannedAt = DateTimeOffset.Now };
        try
        {
            if (!Directory.Exists(labelPath))
            {
                result.ScanError = "标签目录不存在";
                return result;
            }
            if (!Directory.Exists(imagePath))
            {
                result.ScanError = "图片目录不存在";
                return result;
            }

            var imageNames = Directory.EnumerateFiles(imagePath, "*", SearchOption.AllDirectories)
                .Where(x => ImageExtensions.Contains(Path.GetExtension(x)))
                .GroupBy(x => Path.GetFileNameWithoutExtension(x) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
            var labelNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(labelPath, "*.json", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.JsonFileCount++;
                var fileName = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                labelNames[fileName] = labelNames.GetValueOrDefault(fileName) + 1;
                try
                {
                    using var stream = File.OpenRead(file);
                    using var document = JsonDocument.Parse(stream);
                    if (!document.RootElement.TryGetProperty("shapes", out var shapes)
                        || shapes.ValueKind != JsonValueKind.Array)
                    {
                        result.InvalidFileCount++;
                        continue;
                    }

                    foreach (var shape in shapes.EnumerateArray())
                    {
                        if (!shape.TryGetProperty("label", out var labelElement)
                            || labelElement.ValueKind != JsonValueKind.String)
                            continue;

                        var label = labelElement.GetString()?.Trim();
                        if (string.IsNullOrWhiteSpace(label)) continue;
                        result.AnnotationCount++;
                        result.ClassCounts[label] = result.ClassCounts.GetValueOrDefault(label) + 1;
                    }
                }
                catch (JsonException)
                {
                    result.InvalidFileCount++;
                }
                catch (IOException)
                {
                    result.InvalidFileCount++;
                }
            }

            foreach (var name in imageNames.Keys.Union(labelNames.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var imageCount = imageNames.GetValueOrDefault(name);
                var labelCount = labelNames.GetValueOrDefault(name);
                result.MatchedFileCount += Math.Min(imageCount, labelCount);
                result.UnmatchedImageFileCount += Math.Max(0, imageCount - labelCount);
                result.UnmatchedLabelFileCount += Math.Max(0, labelCount - imageCount);
            }
            result.MatchingScannedAt = DateTimeOffset.Now;

            result.ClassCounts = result.ClassCounts
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            result.ScanError = exception.Message;
        }

        return result;
    }
}
