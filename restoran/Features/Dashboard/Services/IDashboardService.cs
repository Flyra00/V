using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Dashboard.Services
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
        Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateProductAsync(Product product, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateProductAsync(int id, Product product, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteProductAsync(int id, CancellationToken cancellationToken = default);
        Task<RevenueReportViewModel> GetRevenueReportAsync(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default);
        Task<StockReportViewModel> GetStockReportAsync(CancellationToken cancellationToken = default);
    }
}
