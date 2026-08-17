using System.Text.Json;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class WeightRecordRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public WeightRecordRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DatasetManager",
            "weights.json");
    }

    public async Task<IReadOnlyList<WeightRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return [];
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<WeightRecord>>(stream, _options, cancellationToken) ?? [];
    }

    public async Task SaveAsync(IReadOnlyCollection<WeightRecord> records, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, records, _options, cancellationToken);
        File.Move(temporaryPath, _filePath, true);
    }
}
