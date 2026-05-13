using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Models;
using Restoran.Shared.Results;

namespace Restoran.Features.Payments.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;

        public PaymentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PaymentMethodOption>> GetActiveCustomerMethodsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PaymentMethodOptions
                .AsNoTracking()
                .Where(option => option.IsActive && option.IsCustomerFacing && option.LegacyMethod == PaymentMethod.Tunai)
                .OrderBy(option => option.SortOrder)
                .ThenBy(option => option.DisplayName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentMethodOption>> GetActiveCashierMethodsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PaymentMethodOptions
                .AsNoTracking()
                .Where(option => option.IsActive && option.IsCashierFacing && option.LegacyMethod == PaymentMethod.Tunai)
                .OrderBy(option => option.SortOrder)
                .ThenBy(option => option.DisplayName)
                .ToListAsync(cancellationToken);
        }

        public async Task<OperationResult<Payment>> CreateOrSyncPaymentAsync(
            Transaction transaction,
            decimal amount,
            PaymentMethod legacyMethod,
            PaymentStatus paymentStatus,
            DateTime effectiveAt,
            string proofUrl = "",
            CancellationToken cancellationToken = default)
        {
            var method = await _context.PaymentMethodOptions
                .FirstOrDefaultAsync(option => option.LegacyMethod == legacyMethod, cancellationToken);

            if (method == null)
            {
                return OperationResult<Payment>.Failure($"Metode pembayaran {legacyMethod} belum tersedia");
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(entity => entity.TransactionId == transaction.Id, cancellationToken);

            if (payment == null)
            {
                payment = new Payment
                {
                    TransactionId = transaction.Id,
                    CreatedAt = effectiveAt
                };
                _context.Payments.Add(payment);
            }

            payment.PaymentMethodOptionId = method.Id;
            payment.Amount = amount;
            payment.PaymentStatus = paymentStatus;
            payment.PaymentDate = paymentStatus == PaymentStatus.Paid ? effectiveAt : null;
            payment.AmountReceived = paymentStatus == PaymentStatus.Paid ? amount : payment.AmountReceived;
            payment.ChangeAmount = paymentStatus == PaymentStatus.Paid ? 0 : payment.ChangeAmount;
            payment.ProofUrl = proofUrl;
            payment.UpdatedAt = effectiveAt;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult<Payment>.Success(payment);
        }

        public async Task<OperationResult> UpdatePaymentProofAsync(int transactionId, string proofUrl, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .Include(entity => entity.Payment)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (transaction.Payment == null)
            {
                return OperationResult.Failure("Data pembayaran tidak ditemukan");
            }

            transaction.Payment!.ProofUrl = proofUrl;
            transaction.Payment.PaymentStatus = PaymentStatus.Pending;
            transaction.Payment.PaymentDate = null;
            transaction.Payment.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Bukti pembayaran berhasil diupload");
        }

        public async Task<OperationResult> MarkPaymentPaidAsync(
            int transactionId,
            DateTime paidAt,
            decimal? amountReceived = null,
            decimal? changeAmount = null,
            CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .Include(entity => entity.Payment)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (transaction.Payment == null)
            {
                return OperationResult.Failure("Data pembayaran tidak ditemukan");
            }

            transaction.Payment.PaymentStatus = PaymentStatus.Paid;
            transaction.Payment.PaymentDate = paidAt;
            transaction.Payment.Amount = transaction.Total;
            transaction.Payment.AmountReceived = amountReceived ?? transaction.Total;
            transaction.Payment.ChangeAmount = changeAmount ?? 0;
            transaction.Payment.UpdatedAt = paidAt;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Pembayaran berhasil dikonfirmasi");
        }
    }
}
