using Restoran.Models;
using Restoran.Shared.Results;

namespace Restoran.Features.Supervisor.Services
{
    public interface ISupervisorService
    {
        Task<IReadOnlyList<Asset>> GetAssetsAsync(CancellationToken cancellationToken = default);
        Task<Asset?> GetAssetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateAssetAsync(Asset asset, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateAssetAsync(int id, Asset asset, CancellationToken cancellationToken = default);
    }
}
