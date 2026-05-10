using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Orders.Dtos;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Orders.Services
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITransactionNumberGenerator _transactionNumberGenerator;
        private readonly IPaymentProofStorage _paymentProofStorage;
        private readonly IChargeConfigurationProvider _chargeConfigurationProvider;
        private readonly ITableService _tableService;

        public OrderService(
            ApplicationDbContext context,
            IDateTimeProvider dateTimeProvider,
            ITransactionNumberGenerator transactionNumberGenerator,
            IPaymentProofStorage paymentProofStorage,
            IChargeConfigurationProvider chargeConfigurationProvider,
            ITableService tableService)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _transactionNumberGenerator = transactionNumberGenerator;
            _paymentProofStorage = paymentProofStorage;
            _chargeConfigurationProvider = chargeConfigurationProvider;
            _tableService = tableService;
        }

        public async Task<OrderMenuViewModel?> GetMenuAsync(int tableId, CancellationToken cancellationToken = default)
        {
            var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken);
            if (table == null)
            {
                return null;
            }

            await _tableService.EnsureActiveSessionAsync(tableId, CustomerType.Guest, null, null, cancellationToken);

            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
            var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);

            return new OrderMenuViewModel
            {
                TableId = tableId,
                TableNumber = table.TableNumber,
                TaxName = chargeConfiguration.TaxName,
                TaxRate = chargeConfiguration.TaxRate,
                ServiceChargeName = chargeConfiguration.ServiceChargeName,
                ServiceChargeRate = chargeConfiguration.ServiceChargeRate,
                ProductsByCategory = products
                    .GroupBy(p => p.Category.Name)
                    .ToDictionary(group => group.Key, group => group.ToList())
            };
        }

        public async Task<OperationResult<CreateOrderResponse>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return OperationResult<CreateOrderResponse>.Failure("Pesanan tidak boleh kosong");
            }

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == request.TableId, cancellationToken);
            if (table == null)
            {
                return OperationResult<CreateOrderResponse>.NotFound("Meja tidak ditemukan");
            }

            var now = _dateTimeProvider.Now;
            var transactionNumber = await _transactionNumberGenerator.GenerateAsync(cancellationToken);
            var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var activeSession = await _tableService.EnsureActiveSessionAsync(
                    request.TableId,
                    request.IsMember ? CustomerType.Member : CustomerType.Guest,
                    request.MemberId,
                    request.CustomerName,
                    cancellationToken);

                var transaction = new Transaction
                {
                    TransactionNumber = transactionNumber,
                    TableId = request.TableId,
                    TableSessionId = activeSession.Id,
                    CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Guest" : request.CustomerName,
                    CustomerType = request.IsMember ? CustomerType.Member : CustomerType.Guest,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = PaymentStatus.Pending,
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
                    return OperationResult<CreateOrderResponse>.Failure("Item pesanan tidak valid");
                }

                transaction.Subtotal = subtotal;
                var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);
                transaction.Tax = chargeConfiguration.CalculateTax(subtotal);
                transaction.ServiceCharge = chargeConfiguration.CalculateServiceCharge(subtotal);

                if (request.IsMember)
                {
                    Member? member = null;

                    if (request.MemberId.HasValue)
                    {
                        member = await _context.Members.FindAsync([request.MemberId.Value], cancellationToken);
                    }

                    if (member == null && request.MemberId.HasValue)
                    {
                        member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == request.MemberId.Value, cancellationToken);
                    }

                    if (member != null)
                    {
                        transaction.Discount = subtotal * (member.DiscountPercentage / 100);
                    }
                }

                transaction.Total = transaction.Subtotal + transaction.Tax + transaction.ServiceCharge - transaction.Discount;

                _context.Notifications.Add(new Notification
                {
                    TransactionId = transaction.Id,
                    Type = NotificationType.NewOrder,
                    Message = $"Pesanan baru dari Meja {table.TableNumber} - {transaction.TransactionNumber}",
                    Recipient = "BagianMasak",
                    CreatedAt = now
                });

                await _context.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                return OperationResult<CreateOrderResponse>.Success(new CreateOrderResponse
                {
                    TransactionId = transaction.Id,
                    TransactionNumber = transaction.TransactionNumber
                });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return OperationResult<CreateOrderResponse>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> UploadPaymentProofAsync(int transactionId, IFormFile paymentProof, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions.FindAsync([transactionId], cancellationToken);
            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (paymentProof == null || paymentProof.Length == 0)
            {
                return OperationResult.Failure("File tidak valid");
            }

            try
            {
                transaction.PaymentProofUrl = await _paymentProofStorage.SaveAsync(
                    transaction.TransactionNumber,
                    paymentProof,
                    cancellationToken);
                transaction.PaymentStatus = PaymentStatus.Pending;

                await _context.SaveChangesAsync(cancellationToken);
                return OperationResult.Success("Bukti pembayaran berhasil diupload");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<Transaction?> GetConfirmationAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Table)
                .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        }
    }
}
