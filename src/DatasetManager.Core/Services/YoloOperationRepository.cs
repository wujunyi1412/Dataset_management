using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class YoloOperationRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public YoloOperationRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DatasetManager",
            "yolo-operations.json");
    }

    public async Task<IReadOnlyList<YoloOperationRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return [];
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<YoloOperationRecord>>(stream, _options, cancellationToken) ?? [];
    }

    public async Task SaveAsync(IReadOnlyCollection<YoloOperationRecord> records, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, records, _options, cancellationToken);
        File.Move(temporaryPath, _filePath, true);
    }
}
