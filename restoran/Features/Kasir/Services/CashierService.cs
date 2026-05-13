using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Kasir.Dtos;
using Restoran.Features.Payments.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;

namespace Restoran.Features.Kasir.Services
{
    public class CashierService : ICashierService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITransactionNumberGenerator _transactionNumberGenerator;
        private readonly IChargeConfigurationProvider _chargeConfigurationProvider;
        private readonly ITableService _tableService;
        private readonly IPaymentService _paymentService;
        private readonly IMidtransService _midtransService;

        public CashierService(
            ApplicationDbContext context,
            IDateTimeProvider dateTimeProvider,
            ITransactionNumberGenerator transactionNumberGenerator,
            IChargeConfigurationProvider chargeConfigurationProvider,
            ITableService tableService,
            IPaymentService paymentService,
            IMidtransService midtransService)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _transactionNumberGenerator = transactionNumberGenerator;
            _chargeConfigurationProvider = chargeConfigurationProvider;
            _tableService = tableService;
            _paymentService = paymentService;
            _midtransService = midtransService;
        }

        public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Table)
                .Include(t => t.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                .Where(t => t.Payment != null &&
                            (t.Payment.PaymentStatus == PaymentStatus.Pending || t.Payment.PaymentStatus == PaymentStatus.Paid))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Product>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.IsAvailable)
                .OrderBy(p => p.CategoryId)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Table>> GetAvailableTablesAsync(CancellationToken cancellationToken = default)
        {
            return await _tableService.GetAvailableTablesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<PaymentMethodOption>> GetAvailablePaymentMethodsAsync(CancellationToken cancellationToken = default)
        {
            return _paymentService.GetActiveCashierMethodsAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Promo>> GetActivePromosAsync(CancellationToken cancellationToken = default)
        {
            var now = _dateTimeProvider.Now;
            var promos = await _context.Promos
                .AsNoTracking()
                .Where(promo => promo.IsActive && promo.StartsAt <= now && promo.EndsAt >= now)
                .ToListAsync(cancellationToken);

            return promos
                .OrderBy(promo => promo.Name)
                .ThenBy(promo => promo.EndsAt)
                .ToList();
        }

        public async Task<OperationResult> ConfirmPaymentAsync(int transactionId, decimal? amountReceived, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .Include(entity => entity.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (transaction.Payment == null)
            {
                return OperationResult.Failure("Data pembayaran tidak ditemukan");
            }

            if (transaction.Payment.PaymentMethodOption.LegacyMethod != PaymentMethod.Tunai)
            {
                return OperationResult.Failure("Pembayaran online harus dicek melalui Midtrans Sandbox. Gunakan tombol Cek Status Midtrans.");
            }

            if (!amountReceived.HasValue)
            {
                return OperationResult.Failure("Uang diterima wajib diisi untuk pembayaran tunai.");
            }

            if (amountReceived.Value < transaction.Total)
            {
                return OperationResult.Failure("Uang diterima tidak boleh kurang dari total pembayaran.");
            }

            var changeAmount = amountReceived.Value - transaction.Total;
            var result = await _paymentService.MarkPaymentPaidAsync(
                transactionId,
                _dateTimeProvider.Now,
                amountReceived.Value,
                changeAmount,
                cancellationToken);
            if (!result.Succeeded)
            {
                return result;
            }

            await _tableService.TryCloseSessionForTransactionAsync(transactionId, cancellationToken);
            return OperationResult.Success($"Pembayaran tunai berhasil. Kembalian: Rp {changeAmount:N0}");
        }

        public async Task<OperationResult> CheckMidtransStatusAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(OperationResult.Failure("Pembayaran Midtrans sedang dinonaktifkan sementara."));
        }

        public async Task<OperationResult> ApplyPromoAsync(int transactionId, int promoId, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .Include(entity => entity.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (transaction.Payment?.PaymentStatus == PaymentStatus.Paid)
            {
                return OperationResult.Failure("Promo tidak dapat diubah karena transaksi sudah lunas.");
            }

            var now = _dateTimeProvider.Now;
            var promo = await _context.Promos
                .FirstOrDefaultAsync(entity => entity.Id == promoId && entity.IsActive && entity.StartsAt <= now && entity.EndsAt >= now, cancellationToken);

            if (promo == null)
            {
                return OperationResult.Failure("Promo tidak aktif atau sudah kedaluwarsa.");
            }

            var subtotal = transaction.Subtotal;
            var discount = promo.PromoType switch
            {
                PromoType.Percentage => subtotal * (promo.DiscountValue / 100m),
                _ => 0m
            };

            discount = Math.Clamp(decimal.Round(discount, 2, MidpointRounding.AwayFromZero), 0m, subtotal);

            transaction.PromoId = promo.Id;
            transaction.AppliedPromoName = promo.Name;
            transaction.Discount = discount;

            await RecalculateTransactionTotalsAsync(transaction, cancellationToken);

            return OperationResult.Success("Promo berhasil diterapkan.");
        }

        public async Task<OperationResult> RemovePromoAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .Include(entity => entity.Payment)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (transaction.Payment?.PaymentStatus == PaymentStatus.Paid)
            {
                return OperationResult.Failure("Promo tidak dapat diubah karena transaksi sudah lunas.");
            }

            transaction.PromoId = null;
            transaction.AppliedPromoName = string.Empty;
            transaction.Discount = 0;

            await RecalculateTransactionTotalsAsync(transaction, cancellationToken);

            return OperationResult.Success("Promo berhasil dihapus.");
        }

        public async Task<Transaction?> GetTransactionForReceiptAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(entity => entity.Table)
                .Include(entity => entity.User)
                .Include(entity => entity.Promo)
                .Include(entity => entity.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .Include(entity => entity.TransactionDetails)
                    .ThenInclude(detail => detail.Product)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);
        }

        public async Task<OperationResult<PosOrderResponse>> CreatePosOrderAsync(
            CreatePosOrderRequest request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (request.Items.Count == 0)
            {
                return OperationResult<PosOrderResponse>.Failure("Pesanan tidak boleh kosong");
            }

            var now = _dateTimeProvider.Now;
            var transactionNumber = await _transactionNumberGenerator.GenerateAsync(cancellationToken);
            if (request.PaymentMethod != PaymentMethod.Tunai)
            {
                return OperationResult<PosOrderResponse>.Failure("Pembayaran online sedang dinonaktifkan sementara. Silakan gunakan pembayaran tunai.");
            }

            var availablePaymentMethod = (await _paymentService.GetActiveCashierMethodsAsync(cancellationToken))
                .Any(method => method.LegacyMethod == request.PaymentMethod);
            if (!availablePaymentMethod)
            {
                return OperationResult<PosOrderResponse>.Failure("Metode pembayaran tidak tersedia");
            }

            var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            OperationResult<PosOrderResponse> operationResult = OperationResult<PosOrderResponse>.Failure("Pesanan gagal diproses");

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    TableSession? activeSession = null;
                    if (request.TableId.HasValue)
                    {
                        activeSession = await _tableService.EnsureActiveSessionAsync(
                            request.TableId.Value,
                            CustomerType.Guest,
                            null,
                            request.CustomerName,
                            cancellationToken);
                    }

                var transaction = new Transaction
                {
                    TransactionNumber = transactionNumber,
                    TableId = request.TableId,
                    TableSessionId = activeSession?.Id,
                    UserId = userId,
                    CustomerName = request.CustomerName,
                    CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : request.CustomerPhone.Trim(),
                    CustomerType = CustomerType.Guest,
                    OrderStatus = OrderStatus.New,
                    CreatedAt = now
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                decimal subtotal = 0;
                foreach (var item in request.Items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        continue;
                    }

                    _context.TransactionDetails.Add(new TransactionDetail
                    {
                        TransactionId = transaction.Id,
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price,
                        Notes = item.Notes,
                        Status = DetailStatus.Pending,
                        CreatedAt = now
                    });

                    subtotal += item.Quantity * product.Price;
                }

                    if (subtotal <= 0)
                    {
                        operationResult = OperationResult<PosOrderResponse>.Failure("Item pesanan tidak valid");
                        await dbTransaction.RollbackAsync(cancellationToken);
                        return;
                    }

                transaction.Subtotal = subtotal;
                var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);
                transaction.Tax = chargeConfiguration.CalculateTax(subtotal);
                transaction.ServiceCharge = chargeConfiguration.CalculateServiceCharge(subtotal);

                if (request.PromoId.HasValue)
                {
                    var promo = await _context.Promos
                        .FirstOrDefaultAsync(entity =>
                            entity.Id == request.PromoId.Value &&
                            entity.IsActive &&
                            entity.StartsAt <= now &&
                            entity.EndsAt >= now,
                            cancellationToken);

                    if (promo == null)
                    {
                        operationResult = OperationResult<PosOrderResponse>.Failure("Promo tidak aktif atau sudah kedaluwarsa.");
                        await dbTransaction.RollbackAsync(cancellationToken);
                        return;
                    }

                    if (subtotal < promo.MinimumPurchase)
                    {
                        operationResult = OperationResult<PosOrderResponse>.Failure($"Minimum pembelian untuk promo {promo.Name} adalah Rp {promo.MinimumPurchase:N0}.");
                        await dbTransaction.RollbackAsync(cancellationToken);
                        return;
                    }

                    var discount = promo.PromoType switch
                    {
                        PromoType.Percentage => subtotal * (promo.DiscountValue / 100m),
                        _ => 0m
                    };

                    transaction.PromoId = promo.Id;
                    transaction.AppliedPromoName = promo.Name;
                    transaction.Discount = Math.Clamp(decimal.Round(discount, 2, MidpointRounding.AwayFromZero), 0m, subtotal);
                }
                else
                {
                    transaction.PromoId = null;
                    transaction.AppliedPromoName = string.Empty;
                    transaction.Discount = 0m;
                }

                transaction.Total = Math.Max(0m, transaction.Subtotal + transaction.Tax + transaction.ServiceCharge - transaction.Discount);
                var initialPaymentStatus = PaymentStatus.Pending;
                var paymentResult = await _paymentService.CreateOrSyncPaymentAsync(
                    transaction,
                    transaction.Total,
                    request.PaymentMethod,
                    initialPaymentStatus,
                    now,
                    string.Empty,
                    cancellationToken);

                    if (!paymentResult.Succeeded)
                    {
                        operationResult = OperationResult<PosOrderResponse>.Failure(paymentResult.Message);
                        await dbTransaction.RollbackAsync(cancellationToken);
                        return;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);

                    operationResult = OperationResult<PosOrderResponse>.Success(new PosOrderResponse
                    {
                        TransactionId = transaction.Id,
                        Total = transaction.Total
                    });
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    operationResult = OperationResult<PosOrderResponse>.Failure(GetDetailedErrorMessage(ex));
                }
            });

            return operationResult;
        }

        private static string GetDetailedErrorMessage(Exception ex)
        {
            var root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return string.Equals(root.Message, ex.Message, StringComparison.Ordinal)
                ? ex.Message
                : $"{ex.Message} | Root cause: {root.Message}";
        }

        private async Task RecalculateTransactionTotalsAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);

            // Konsisten dengan flow existing: pajak dan service dihitung dari subtotal sebelum diskon.
            transaction.Tax = chargeConfiguration.CalculateTax(transaction.Subtotal);
            transaction.ServiceCharge = chargeConfiguration.CalculateServiceCharge(transaction.Subtotal);
            transaction.Total = Math.Max(0, transaction.Subtotal + transaction.Tax + transaction.ServiceCharge - transaction.Discount);

            if (transaction.Payment != null)
            {
                transaction.Payment.Amount = transaction.Total;
                transaction.Payment.UpdatedAt = _dateTimeProvider.Now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
