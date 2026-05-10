using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Supervisor.Dtos;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Supervisor.Services
{
    public class AssetLogService : IAssetLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public AssetLogService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<IReadOnlyList<AssetLookupItem>> GetAvailableAssetsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Assets
                .AsNoTracking()
                .Where(a => a.Quantity > 0)
                .OrderBy(a => a.Name)
                .Select(a => new AssetLookupItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    Quantity = a.Quantity,
                    Unit = a.Unit
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<OperationResult> CreateAsync(AssetLogViewModel model, int reporterUserId, CancellationToken cancellationToken = default)
        {
            var asset = await _context.Assets.FindAsync([model.AssetId], cancellationToken);
            if (asset == null)
            {
                return OperationResult.Failure("Aset tidak ditemukan");
            }

            if (model.Quantity > asset.Quantity)
            {
                return OperationResult.Failure("Kuantitas melebihi stok tersedia");
            }

            var reporter = await _context.Users.FindAsync([reporterUserId], cancellationToken);
            var now = _dateTimeProvider.Now;

            var assetLog = new AssetLog
            {
                AssetId = model.AssetId,
                DamageType = model.DamageType,
                Quantity = model.Quantity,
                ReportedBy = reporter?.Id ?? 1,
                Description = model.Description,
                Status = LogStatus.Reported,
                ReportedAt = now
            };

            _context.AssetLogs.Add(assetLog);

            asset.Quantity -= model.Quantity;
            if (asset.Quantity == 0 || model.DamageType == DamageType.Hilang)
            {
                asset.Condition = AssetCondition.Hilang;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _context.Notifications.Add(new Notification
            {
                Type = NotificationType.AssetDamage,
                Message = $"Laporan {model.DamageType}: {asset.Name} - {model.Quantity} {asset.Unit} (Dilaporkan oleh: {reporter?.Username ?? "Supervisor"})",
                Recipient = "Owner",
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Laporan kerusakan berhasil disimpan dan stok telah dikurangi");
        }

        public async Task<IReadOnlyList<AssetLogListViewModel>> GetLogsAsync(DateTime? fromDate, DateTime? toDate, LogStatus? status, CancellationToken cancellationToken = default)
        {
            var query = _context.AssetLogs
                .AsNoTracking()
                .Include(al => al.Asset)
                .Include(al => al.Reporter)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(al => al.ReportedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(al => al.ReportedAt <= toDate.Value.AddDays(1));
            }

            if (status.HasValue)
            {
                query = query.Where(al => al.Status == status.Value);
            }

            return await query
                .OrderByDescending(al => al.ReportedAt)
                .Select(al => new AssetLogListViewModel
                {
                    Id = al.Id,
                    AssetName = al.Asset.Name,
                    DamageType = al.DamageType,
                    Quantity = al.Quantity,
                    Unit = al.Asset.Unit,
                    ReporterName = al.Reporter.Username,
                    Description = al.Description,
                    ReportedAt = al.ReportedAt,
                    Status = al.Status
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<OperationResult> ApproveAsync(int id, int approverUserId, CancellationToken cancellationToken = default)
        {
            var assetLog = await _context.AssetLogs.FirstOrDefaultAsync(al => al.Id == id, cancellationToken);
            if (assetLog == null)
            {
                return OperationResult.NotFound("Laporan tidak ditemukan");
            }

            assetLog.Status = LogStatus.Approved;
            assetLog.ApprovedBy = approverUserId;
            assetLog.ApprovedAt = _dateTimeProvider.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Laporan telah disetujui");
        }

        public async Task<AssetDamageReportViewModel> GetReportAsync(CancellationToken cancellationToken = default)
        {
            var lastThirtyDays = _dateTimeProvider.Now.AddDays(-30);

            return new AssetDamageReportViewModel
            {
                TotalDamageReports = await _context.AssetLogs.CountAsync(cancellationToken),
                TotalApproved = await _context.AssetLogs.CountAsync(al => al.Status == LogStatus.Approved, cancellationToken),
                TotalPending = await _context.AssetLogs.CountAsync(al => al.Status == LogStatus.Reported, cancellationToken),
                RecentDamages = await _context.AssetLogs
                    .AsNoTracking()
                    .Include(al => al.Asset)
                    .Where(al => al.ReportedAt >= lastThirtyDays)
                    .OrderByDescending(al => al.ReportedAt)
                    .Take(10)
                    .Select(al => new AssetDamageItemViewModel
                    {
                        AssetName = al.Asset.Name,
                        DamageType = al.DamageType,
                        Quantity = al.Quantity,
                        Unit = al.Asset.Unit,
                        ReportedAt = al.ReportedAt
                    })
                    .ToListAsync(cancellationToken),
                DamageByType = await _context.AssetLogs
                    .AsNoTracking()
                    .GroupBy(al => al.DamageType)
                    .Select(g => new DamageTypeSummaryViewModel
                    {
                        DamageType = g.Key,
                        Count = g.Count(),
                        TotalQuantity = g.Sum(al => al.Quantity)
                    })
                    .ToListAsync(cancellationToken)
            };
        }
    }
}
