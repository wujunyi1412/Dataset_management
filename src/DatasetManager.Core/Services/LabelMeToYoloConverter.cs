using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DatasetManager.Core.Services;

public sealed class LabelMeToYoloConverter
{
    public async Task<YoloConversionResult> ConvertAsync(
        string sourceDirectory,
        string outputDirectory,
        IReadOnlyList<string> categories,
        IProgress<YoloConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException($"LabelMe 目录不存在：{sourceDirectory}");
        if (categories.Count == 0) throw new ArgumentException("至少输入一个需要转换的标签类别。", nameof(categories));
        var normalizedCategories = categories
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedCategories.Length == 0) throw new ArgumentException("至少输入一个有效标签类别。", nameof(categories));

        Directory.CreateDirectory(outputDirectory);
        var classIds = normalizedCategories.Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);
        var jsonFiles = Directory.EnumerateFiles(sourceDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = new YoloConversionResult { JsonFileCount = jsonFiles.Length };

        for (var fileIndex = 0; fileIndex < jsonFiles.Length; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jsonPath = jsonFiles[fileIndex];
            try
            {
                using var stream = File.OpenRead(jsonPath);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (!TryGetPositiveNumber(root, "imageWidth", out var imageWidth)
                    || !TryGetPositiveNumber(root, "imageHeight", out var imageHeight)
                    || !root.TryGetProperty("shapes", out var shapes)
                    || shapes.ValueKind != JsonValueKind.Array)
                {
                    result.InvalidFileCount++;
                    continue;
                }

                var lines = new List<string>();
                foreach (var shape in shapes.EnumerateArray())
                {
                    if (!shape.TryGetProperty("label", out var labelElement)
                        || labelElement.ValueKind != JsonValueKind.String)
                        continue;
                    var label = labelElement.GetString()?.Trim();
                    if (label is null || !classIds.TryGetValue(label, out var classId)) continue;
                    if (!shape.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array) continue;

                    var coordinates = new List<(double X, double Y)>();
                    foreach (var point in points.EnumerateArray())
                    {
                        if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2) continue;
                        if (point[0].TryGetDouble(out var x) && point[1].TryGetDouble(out var y)) coordinates.Add((x, y));
                    }
                    if (coordinates.Count < 2) continue;
                    var minX = Math.Clamp(coordinates.Min(x => x.X), 0, imageWidth);
                    var maxX = Math.Clamp(coordinates.Max(x => x.X), 0, imageWidth);
                    var minY = Math.Clamp(coordinates.Min(x => x.Y), 0, imageHeight);
                    var maxY = Math.Clamp(coordinates.Max(x => x.Y), 0, imageHeight);
                    if (maxX <= minX || maxY <= minY) continue;

                    var centerX = (minX + maxX) / 2 / imageWidth;
                    var centerY = (minY + maxY) / 2 / imageHeight;
                    var width = (maxX - minX) / imageWidth;
                    var height = (maxY - minY) / imageHeight;
                    lines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{classId} {centerX:0.######} {centerY:0.######} {width:0.######} {height:0.######}"));
                    result.ConvertedAnnotationCount++;
                    result.CategoryCounts[label] = result.CategoryCounts.GetValueOrDefault(label) + 1;
                }

                var relative = Path.GetRelativePath(sourceDirectory, jsonPath);
                var outputPath = Path.Combine(outputDirectory, Path.ChangeExtension(relative, ".txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllLinesAsync(outputPath, lines, new UTF8Encoding(false), cancellationToken);
                result.OutputFileCount++;
            }
            catch (JsonException)
            {
                result.InvalidFileCount++;
            }
            catch (IOException)
            {
                result.InvalidFileCount++;
            }
            progress?.Report(new YoloConversionProgress(fileIndex + 1, jsonFiles.Length));
        }

        await File.WriteAllLinesAsync(
            Path.Combine(outputDirectory, "classes.txt"),
            normalizedCategories,
            new UTF8Encoding(false),
            cancellationToken);
        result.Categories = normalizedCategories.ToList();
        return result;
    }

    private static bool TryGetPositiveNumber(JsonElement root, string name, out double value)
    {
        value = 0;
        return root.TryGetProperty(name, out var element) && element.TryGetDouble(out value) && value > 0;
    }
}

public sealed class YoloConversionResult
{
    public int JsonFileCount { get; set; }
    public int OutputFileCount { get; set; }
    public int ConvertedAnnotationCount { get; set; }
    public int InvalidFileCount { get; set; }
    public List<string> Categories { get; set; } = [];
    public Dictionary<string, int> CategoryCounts { get; set; } = new(StringComparer.Ordinal);
}

public readonly record struct YoloConversionProgress(int Completed, int Total);
