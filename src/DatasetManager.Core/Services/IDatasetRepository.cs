using DatasetManager.Core.Models;

namespace DatasetManager.Core.Services;

public interface IDatasetRepository
{
    Task<IReadOnlyList<DatasetRecord>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyCollection<DatasetRecord> datasets, CancellationToken cancellationToken = default);
}
