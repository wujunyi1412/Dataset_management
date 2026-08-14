using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class DatasetScanner(IImageMetadataReader? metadataReader = null)
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"
    };

    public Task<DatasetStatistics> ScanAsync(string rootPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(rootPath, cancellationToken), cancellationToken);

    private DatasetStatistics Scan(string rootPath, CancellationToken cancellationToken)
    {
        var result = new DatasetStatistics { ScannedAt = DateTimeOffset.Now };
        try
        {
            if (!Directory.Exists(rootPath))
            {
                result.ScanError = "目录不存在";
                return result;
            }

            result.SubfolderCount = Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories).Count();
            var folders = new Dictionary<string, FolderImageStatistics>(StringComparer.OrdinalIgnoreCase);
            var resolutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bitDepths = new HashSet<int>();
            var channelCounts = new HashSet<int>();

            foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extension = Path.GetExtension(file);
                if (!ImageExtensions.Contains(extension)) continue;

                result.ImageCount++;
                var directory = Path.GetDirectoryName(file) ?? rootPath;
                var relativePath = Path.GetRelativePath(rootPath, directory);
                if (relativePath == ".") relativePath = string.Empty;
                if (!folders.TryGetValue(relativePath, out var folder))
                {
                    folder = new FolderImageStatistics { RelativePath = relativePath };
                    folders.Add(relativePath, folder);
                }
                folder.ImageCount++;
                var normalizedExtension = extension.ToLowerInvariant();
                folder.ExtensionCounts[normalizedExtension] =
                    folder.ExtensionCounts.GetValueOrDefault(normalizedExtension) + 1;

                if (metadataReader is null || !metadataReader.TryRead(file, out var metadata))
                {
                    result.UnreadableImageCount++;
                    continue;
                }
                resolutions.Add($"{metadata.Width}×{metadata.Height}");
                bitDepths.Add(metadata.BitDepth);
                channelCounts.Add(metadata.Channels);
            }

            result.ImageFolders = folders.Values
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Resolutions = resolutions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            result.BitDepths = bitDepths.Order().ToList();
            result.ChannelCounts = channelCounts.Order().ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            result.ScanError = exception.Message;
        }

        return result;
    }
}
