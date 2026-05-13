using Restoran.Features.Kasir.Dtos;
using Restoran.Models;
using Restoran.Shared.Results;

namespace Restoran.Features.Kasir.Services
{
    public interface ICashierService
    {
        Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Product>> GetAvailableProductsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Table>> GetAvailableTablesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PaymentMethodOption>> GetAvailablePaymentMethodsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Promo>> GetActivePromosAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> ConfirmPaymentAsync(int transactionId, decimal? amountReceived, CancellationToken cancellationToken = default);
        Task<OperationResult> CheckMidtransStatusAsync(int transactionId, CancellationToken cancellationToken = default);
        Task<OperationResult> ApplyPromoAsync(int transactionId, int promoId, CancellationToken cancellationToken = default);
        Task<OperationResult> RemovePromoAsync(int transactionId, CancellationToken cancellationToken = default);
        Task<Transaction?> GetTransactionForReceiptAsync(int transactionId, CancellationToken cancellationToken = default);
        Task<OperationResult<PosOrderResponse>> CreatePosOrderAsync(
            CreatePosOrderRequest request,
            int userId,
            CancellationToken cancellationToken = default);
    }
}
