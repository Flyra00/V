using Restoran.Models;
using Restoran.Shared.Results;

namespace Restoran.Features.Catalog.Services
{
    public interface ICategoryService
    {
        Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
        Task<Category?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateCategoryAsync(Category category, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateCategoryAsync(int id, Category category, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    }
}
