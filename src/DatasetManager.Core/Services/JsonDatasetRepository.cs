using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class JsonDatasetRepository : IDatasetRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonDatasetRepository(string? filePath = null)
    {
        _filePath = filePath ?? ApplicationDataPaths.GetRecordFile("catalog.json");
    }

    public async Task<IReadOnlyList<DatasetRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return [];

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<DatasetRecord>>(stream, _options, cancellationToken)
            ?? [];
    }

    public async Task SaveAsync(IReadOnlyCollection<DatasetRecord> datasets, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, datasets, _options, cancellationToken);
        }

        File.Move(temporaryPath, _filePath, true);
    }
}
