using Restoran.Models;
using Restoran.Shared.Results;

namespace Restoran.Features.Payments.Services
{
    public interface IPaymentService
    {
        Task<IReadOnlyList<PaymentMethodOption>> GetActiveCustomerMethodsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PaymentMethodOption>> GetActiveCashierMethodsAsync(CancellationToken cancellationToken = default);
        Task<OperationResult<Payment>> CreateOrSyncPaymentAsync(
            Transaction transaction,
            decimal amount,
            PaymentMethod legacyMethod,
            PaymentStatus paymentStatus,
            DateTime effectiveAt,
            string proofUrl = "",
            CancellationToken cancellationToken = default);
        Task<OperationResult> UpdatePaymentProofAsync(int transactionId, string proofUrl, CancellationToken cancellationToken = default);
        Task<OperationResult> MarkPaymentPaidAsync(
            int transactionId,
            DateTime paidAt,
            decimal? amountReceived = null,
            decimal? changeAmount = null,
            CancellationToken cancellationToken = default);
    }
}
