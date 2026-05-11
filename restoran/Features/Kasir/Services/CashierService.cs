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

        public CashierService(
            ApplicationDbContext context,
            IDateTimeProvider dateTimeProvider,
            ITransactionNumberGenerator transactionNumberGenerator,
            IChargeConfigurationProvider chargeConfigurationProvider,
            ITableService tableService,
            IPaymentService paymentService)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _transactionNumberGenerator = transactionNumberGenerator;
            _chargeConfigurationProvider = chargeConfigurationProvider;
            _tableService = tableService;
            _paymentService = paymentService;
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

        public async Task<OperationResult> ConfirmPaymentAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            var result = await _paymentService.MarkPaymentPaidAsync(transactionId, _dateTimeProvider.Now, cancellationToken);
            if (!result.Succeeded)
            {
                return result;
            }

            await _tableService.TryCloseSessionForTransactionAsync(transactionId, cancellationToken);
            return OperationResult.Success("Pembayaran berhasil dikonfirmasi");
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
                    await dbTransaction.RollbackAsync(cancellationToken);
                    return OperationResult<PosOrderResponse>.Failure("Item pesanan tidak valid");
                }

                transaction.Subtotal = subtotal;
                var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);
                transaction.Tax = chargeConfiguration.CalculateTax(subtotal);
                transaction.ServiceCharge = chargeConfiguration.CalculateServiceCharge(subtotal);
                transaction.Total = transaction.Subtotal + transaction.Tax + transaction.ServiceCharge;
                var initialPaymentStatus = request.PaymentMethod == PaymentMethod.Tunai ? PaymentStatus.Paid : PaymentStatus.Pending;
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
                    await dbTransaction.RollbackAsync(cancellationToken);
                    return OperationResult<PosOrderResponse>.Failure(paymentResult.Message);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                return OperationResult<PosOrderResponse>.Success(new PosOrderResponse
                {
                    TransactionId = transaction.Id,
                    Total = transaction.Total
                });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return OperationResult<PosOrderResponse>.Failure(ex.Message);
            }
        }
    }
}
