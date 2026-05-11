using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Tables.Services
{
    public class TableService : ITableService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public TableService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<IReadOnlyList<CustomerTableOptionViewModel>> GetCustomerTableOptionsAsync(CancellationToken cancellationToken = default)
        {
            var activeSessionTableIds = await _context.TableSessions
                .AsNoTracking()
                .Where(session => session.Status == TableSessionStatus.Active && session.EndTime == null)
                .Select(session => session.TableId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return await _context.Tables
                .AsNoTracking()
                .OrderBy(table => table.TableNumber)
                .Select(table => new CustomerTableOptionViewModel
                {
                    Id = table.Id,
                    TableNumber = table.TableNumber,
                    Capacity = table.Capacity,
                    Status = activeSessionTableIds.Contains(table.Id) ? TableStatus.Occupied : table.Status,
                    StatusLabel = activeSessionTableIds.Contains(table.Id)
                        ? "Sedang Dipakai"
                        : table.Status == TableStatus.Reserved ? "Reservasi" : "Tersedia",
                    CanStartOrder = !activeSessionTableIds.Contains(table.Id) && table.Status != TableStatus.Reserved
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Table>> GetAvailableTablesAsync(CancellationToken cancellationToken = default)
        {
            var activeSessionTableIds = await _context.TableSessions
                .AsNoTracking()
                .Where(session => session.Status == TableSessionStatus.Active && session.EndTime == null)
                .Select(session => session.TableId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return await _context.Tables
                .AsNoTracking()
                .Where(table => table.Status == TableStatus.Available && !activeSessionTableIds.Contains(table.Id))
                .OrderBy(table => table.TableNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task<TableManagementViewModel> GetManagementAsync(CancellationToken cancellationToken = default)
        {
            var tables = await _context.Tables
                .AsNoTracking()
                .Include(table => table.TableSessions.Where(session => session.Status == TableSessionStatus.Active && session.EndTime == null))
                    .ThenInclude(session => session.Transactions)
                .OrderBy(table => table.TableNumber)
                .ToListAsync(cancellationToken);

            return new TableManagementViewModel
            {
                Tables = tables.Select(table =>
                {
                    var activeSession = table.TableSessions
                        .OrderByDescending(session => session.StartTime)
                        .FirstOrDefault();
                    var effectiveStatus = activeSession != null ? TableStatus.Occupied : table.Status;

                    return new TableManagementItemViewModel
                    {
                        Id = table.Id,
                        TableNumber = table.TableNumber,
                        Capacity = table.Capacity,
                        Status = effectiveStatus,
                        StatusLabel = activeSession != null
                            ? "Occupied"
                            : table.Status.ToString(),
                        HasActiveSession = activeSession != null,
                        SessionStartedAt = activeSession?.StartTime,
                        SessionCustomerType = activeSession?.CustomerType.ToString() ?? string.Empty,
                        SessionCustomerName = activeSession?.CustomerName ?? string.Empty,
                        ActiveTransactionCount = activeSession?.Transactions.Count(transaction => !IsTerminalOrderStatus(transaction.OrderStatus)) ?? 0,
                        QrCodeUrl = string.IsNullOrWhiteSpace(table.QrCodeUrl)
                            ? $"/QR/Generate?tableId={table.Id}"
                            : table.QrCodeUrl
                    };
                }).ToList()
            };
        }

        public Task<Table?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return _context.Tables
                .AsNoTracking()
                .Include(table => table.TableSessions.Where(session => session.Status == TableSessionStatus.Active && session.EndTime == null))
                .FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
        }

        public async Task<OperationResult> CreateAsync(TableFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (await _context.Tables.AnyAsync(
                    table => table.TableNumber.ToLower() == model.TableNumber.ToLower(),
                    cancellationToken))
            {
                return OperationResult.Failure("Nomor meja sudah digunakan");
            }

            var table = new Table
            {
                TableNumber = model.TableNumber.Trim(),
                Capacity = model.Capacity,
                Status = NormalizeStatus(model.Status),
                CreatedAt = _dateTimeProvider.Now
            };

            _context.Tables.Add(table);
            await _context.SaveChangesAsync(cancellationToken);

            table.QrCodeUrl = $"/QR/Generate?tableId={table.Id}";
            await _context.SaveChangesAsync(cancellationToken);

            return OperationResult.Success("Meja berhasil ditambahkan");
        }

        public async Task<OperationResult> UpdateAsync(int id, TableFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (id != model.Id)
            {
                return OperationResult.NotFound("Meja tidak ditemukan");
            }

            var table = await _context.Tables
                .Include(entity => entity.TableSessions.Where(session => session.Status == TableSessionStatus.Active && session.EndTime == null))
                .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

            if (table == null)
            {
                return OperationResult.NotFound("Meja tidak ditemukan");
            }

            if (await _context.Tables.AnyAsync(
                    entity => entity.Id != id && entity.TableNumber.ToLower() == model.TableNumber.ToLower(),
                    cancellationToken))
            {
                return OperationResult.Failure("Nomor meja sudah digunakan");
            }

            if (table.TableSessions.Any() && model.Status == TableStatus.Available)
            {
                return OperationResult.Failure("Meja yang sedang memiliki sesi aktif tidak bisa diubah menjadi tersedia");
            }

            table.TableNumber = model.TableNumber.Trim();
            table.Capacity = model.Capacity;
            table.Status = table.TableSessions.Any() ? TableStatus.Occupied : NormalizeStatus(model.Status);

            if (string.IsNullOrWhiteSpace(table.QrCodeUrl))
            {
                table.QrCodeUrl = $"/QR/Generate?tableId={table.Id}";
            }

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Meja berhasil diupdate");
        }

        public async Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var table = await _context.Tables
                .Include(entity => entity.Transactions)
                .Include(entity => entity.TableSessions)
                .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

            if (table == null)
            {
                return OperationResult.NotFound("Meja tidak ditemukan");
            }

            if (table.Transactions.Any() || table.TableSessions.Any())
            {
                return OperationResult.Failure("Meja yang sudah memiliki histori transaksi atau sesi tidak dapat dihapus");
            }

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Meja berhasil dihapus");
        }

        public Task<TableSession?> GetActiveSessionAsync(int tableId, CancellationToken cancellationToken = default)
        {
            return _context.TableSessions
                .Include(session => session.Table)
                .FirstOrDefaultAsync(
                    session => session.TableId == tableId &&
                               session.Status == TableSessionStatus.Active &&
                               session.EndTime == null,
                    cancellationToken);
        }

        public async Task<TableSession> EnsureActiveSessionAsync(
            int tableId,
            CustomerType customerType,
            int? memberId,
            string? customerName,
            CancellationToken cancellationToken = default)
        {
            var table = await _context.Tables.FirstOrDefaultAsync(entity => entity.Id == tableId, cancellationToken)
                ?? throw new InvalidOperationException("Meja tidak ditemukan");

            var session = await _context.TableSessions
                .FirstOrDefaultAsync(
                    entity => entity.TableId == tableId &&
                              entity.Status == TableSessionStatus.Active &&
                              entity.EndTime == null,
                    cancellationToken);

            if (session == null)
            {
                session = new TableSession
                {
                    TableId = tableId,
                    CustomerType = customerType,
                    MemberId = memberId,
                    CustomerName = customerName?.Trim() ?? string.Empty,
                    StartTime = _dateTimeProvider.Now,
                    Status = TableSessionStatus.Active
                };

                _context.TableSessions.Add(session);
            }
            else
            {
                if (customerType == CustomerType.Member)
                {
                    session.CustomerType = CustomerType.Member;
                }

                if (memberId.HasValue && !session.MemberId.HasValue)
                {
                    session.MemberId = memberId;
                }

                if (!string.IsNullOrWhiteSpace(customerName) && string.IsNullOrWhiteSpace(session.CustomerName))
                {
                    session.CustomerName = customerName.Trim();
                }
            }

            if (table.Status != TableStatus.Occupied)
            {
                table.Status = TableStatus.Occupied;
            }

            if (string.IsNullOrWhiteSpace(table.QrCodeUrl))
            {
                table.QrCodeUrl = $"/QR/Generate?tableId={table.Id}";
            }

            await _context.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task TryCloseSessionForTransactionAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction?.TableSessionId == null)
            {
                return;
            }

            var session = await _context.TableSessions
                .Include(entity => entity.Table)
                .Include(entity => entity.Transactions)
                    .ThenInclude(transaction => transaction.Payment)
                .FirstOrDefaultAsync(entity => entity.Id == transaction.TableSessionId.Value, cancellationToken);

            if (session == null || session.Status != TableSessionStatus.Active)
            {
                return;
            }

            var hasActiveTransactions = session.Transactions.Any(entity => !IsSessionTerminal(entity));
            if (hasActiveTransactions)
            {
                if (session.Table.Status != TableStatus.Occupied)
                {
                    session.Table.Status = TableStatus.Occupied;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return;
            }

            session.Status = TableSessionStatus.Closed;
            session.EndTime = _dateTimeProvider.Now;

            if (session.Table.Status == TableStatus.Occupied)
            {
                session.Table.Status = TableStatus.Available;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static TableStatus NormalizeStatus(TableStatus status)
            => status == TableStatus.Occupied ? TableStatus.Available : status;

        private static bool IsSessionTerminal(Transaction transaction)
            => transaction.Payment is { PaymentStatus: PaymentStatus.Paid or PaymentStatus.Cancelled } &&
               transaction.OrderStatus is OrderStatus.Served or OrderStatus.Completed or OrderStatus.Cancelled;

        private static bool IsTerminalOrderStatus(OrderStatus status)
            => status is OrderStatus.Served or OrderStatus.Completed or OrderStatus.Cancelled;
    }
}
