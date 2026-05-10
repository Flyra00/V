using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Kitchen.Services
{
    public class KitchenService : IKitchenService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITableService _tableService;

        public KitchenService(
            ApplicationDbContext context,
            IDateTimeProvider dateTimeProvider,
            ITableService tableService)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _tableService = tableService;
        }

        public async Task<IReadOnlyList<KitchenOrderViewModel>> GetDisplayOrdersAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Table)
                .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                .Where(t => t.OrderStatus == OrderStatus.New ||
                            t.OrderStatus == OrderStatus.Processing ||
                            t.OrderStatus == OrderStatus.Ready)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new KitchenOrderViewModel
                {
                    TransactionId = t.Id,
                    TransactionNumber = t.TransactionNumber,
                    TableNumber = t.Table != null ? t.Table.TableNumber : "Takeaway",
                    CreatedAt = t.CreatedAt,
                    OrderStatus = t.OrderStatus,
                    Items = t.TransactionDetails.Select(td => new KitchenItemViewModel
                    {
                        ProductName = td.Product.Name,
                        Quantity = td.Quantity,
                        Notes = td.Notes,
                        Status = td.Status
                    }).ToList()
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<OperationResult> UpdateStatusAsync(int transactionId, OrderStatus status, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .Include(t => t.TransactionDetails)
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return OperationResult.NotFound("Pesanan tidak ditemukan");
            }

            var isValidTransition = transaction.OrderStatus switch
            {
                OrderStatus.New => status == OrderStatus.Processing,
                OrderStatus.Processing => status == OrderStatus.Ready,
                OrderStatus.Ready => status == OrderStatus.Served,
                _ => false
            };

            if (!isValidTransition)
            {
                return OperationResult.Failure("Transisi status pesanan tidak valid");
            }

            transaction.OrderStatus = status;

            foreach (var detail in transaction.TransactionDetails)
            {
                detail.Status = status switch
                {
                    OrderStatus.Processing => DetailStatus.Preparing,
                    OrderStatus.Ready => DetailStatus.Ready,
                    OrderStatus.Served => DetailStatus.Served,
                    _ => detail.Status
                };

                if (status == OrderStatus.Served)
                {
                    detail.CompletedAt = _dateTimeProvider.Now;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _context.Notifications.Add(new Notification
            {
                TransactionId = transaction.Id,
                Type = status == OrderStatus.Ready ? NotificationType.OrderReady : NotificationType.NewOrder,
                Message = $"Pesanan {transaction.TransactionNumber} - {status}",
                Recipient = "Kasir",
                CreatedAt = _dateTimeProvider.Now
            });

            await _context.SaveChangesAsync(cancellationToken);
            await _tableService.TryCloseSessionForTransactionAsync(transaction.Id, cancellationToken);
            return OperationResult.Success();
        }

        public async Task<PrintReceiptViewModel?> GetPrintReceiptAsync(int id, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Table)
                .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            return new PrintReceiptViewModel
            {
                TransactionNumber = transaction.TransactionNumber,
                TableNumber = transaction.Table?.TableNumber ?? "Takeaway",
                CreatedAt = transaction.CreatedAt,
                Items = transaction.TransactionDetails.Select(td => new ReceiptItemViewModel
                {
                    ProductName = td.Product.Name,
                    Quantity = td.Quantity,
                    UnitPrice = td.UnitPrice,
                    Subtotal = td.Subtotal,
                    Notes = td.Notes
                }).ToList(),
                Subtotal = transaction.Subtotal,
                Tax = transaction.Tax,
                ServiceCharge = transaction.ServiceCharge,
                Discount = transaction.Discount,
                Total = transaction.Total
            };
        }

        public async Task<int> GetNewOrdersCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Transactions.CountAsync(t => t.OrderStatus == OrderStatus.New, cancellationToken);
        }
    }
}
