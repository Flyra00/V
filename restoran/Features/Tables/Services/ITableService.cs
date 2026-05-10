using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Tables.Services
{
    public interface ITableService
    {
        Task<IReadOnlyList<CustomerTableOptionViewModel>> GetCustomerTableOptionsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Table>> GetAvailableTablesAsync(CancellationToken cancellationToken = default);
        Task<TableManagementViewModel> GetManagementAsync(CancellationToken cancellationToken = default);
        Task<Table?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateAsync(TableFormViewModel model, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateAsync(int id, TableFormViewModel model, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<TableSession?> GetActiveSessionAsync(int tableId, CancellationToken cancellationToken = default);
        Task<TableSession> EnsureActiveSessionAsync(
            int tableId,
            CustomerType customerType,
            int? memberId,
            string? customerName,
            CancellationToken cancellationToken = default);
        Task TryCloseSessionForTransactionAsync(int transactionId, CancellationToken cancellationToken = default);
    }
}
