using Restoran.Models;
using Restoran.Shared.Results;

namespace Restoran.Features.Supervisor.Services
{
    public interface ISupervisorService
    {
        Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(CancellationToken cancellationToken = default);
        Task<Ingredient?> GetIngredientByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateIngredientAsync(Ingredient ingredient, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateIngredientAsync(int id, Ingredient ingredient, CancellationToken cancellationToken = default);
        Task<OperationResult<decimal>> AddStockAsync(int id, decimal amount, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Asset>> GetAssetsAsync(CancellationToken cancellationToken = default);
        Task<Asset?> GetAssetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateAssetAsync(Asset asset, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateAssetAsync(int id, Asset asset, CancellationToken cancellationToken = default);
    }
}
