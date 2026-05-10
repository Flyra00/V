using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Kitchen.Services
{
    public interface IKitchenService
    {
        Task<IReadOnlyList<KitchenOrderViewModel>> GetDisplayOrdersAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateStatusAsync(int transactionId, OrderStatus status, CancellationToken cancellationToken = default);
        Task<PrintReceiptViewModel?> GetPrintReceiptAsync(int id, CancellationToken cancellationToken = default);
        Task<int> GetNewOrdersCountAsync(CancellationToken cancellationToken = default);
    }
}
