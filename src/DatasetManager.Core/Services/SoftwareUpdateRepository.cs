using System.Text.Json;
using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public sealed class SoftwareUpdateRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public SoftwareUpdateRepository(string? filePath = null)
    {
        _filePath = filePath ?? ApplicationDataPaths.GetRecordFile("software-updates.json");
    }

    public async Task<IReadOnlyList<SoftwareUpdateRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return [];
        await using var stream = File.OpenRead(_filePath);
        var records = await JsonSerializer.DeserializeAsync<List<SoftwareUpdateRecord>>(stream, _options, cancellationToken) ?? [];
        foreach (var record in records)
            record.Snapshot = new Dictionary<string, SoftwareFileSnapshot>(record.Snapshot, StringComparer.OrdinalIgnoreCase);
        return records;
    }

    public async Task SaveAsync(IReadOnlyCollection<SoftwareUpdateRecord> records, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, records, _options, cancellationToken);
        File.Move(temporaryPath, _filePath, true);
    }
}
