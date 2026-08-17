namespace DatasetManager.Core.Services;

public sealed class ImageLabelPairFinder
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"
    };

    public IReadOnlyList<ImageLabelPair> Find(string imageRoot, string labelRoot)
    {
        if (!Directory.Exists(imageRoot)) throw new DirectoryNotFoundException($"图片目录不存在：{imageRoot}");
        if (!Directory.Exists(labelRoot)) throw new DirectoryNotFoundException($"标签目录不存在：{labelRoot}");

        var images = Directory.EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories)
            .Where(x => ImageExtensions.Contains(Path.GetExtension(x)))
            .GroupBy(GetStem, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        var labels = Directory.EnumerateFiles(labelRoot, "*.json", SearchOption.AllDirectories)
            .GroupBy(GetStem, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);

        var pairs = new List<ImageLabelPair>();
        foreach (var name in images.Keys.Intersect(labels.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var imageFiles = images[name];
            var labelFiles = labels[name];
            for (var index = 0; index < Math.Min(imageFiles.Length, labelFiles.Length); index++)
                pairs.Add(new ImageLabelPair(imageFiles[index], labelFiles[index]));
        }
        return pairs;
    }

    private static string GetStem(string path) => Path.GetFileNameWithoutExtension(path) ?? string.Empty;
}

public sealed record ImageLabelPair(string ImagePath, string LabelPath);
