using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;

namespace Restoran.Features.Supervisor.Services
{
    public class SupervisorService : ISupervisorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public SupervisorService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<IReadOnlyList<Asset>> GetAssetsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Assets
                .AsNoTracking()
                .OrderBy(a => a.AssetType)
                .ThenBy(a => a.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Asset?> GetAssetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Assets.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreateAssetAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            asset.CreatedAt = _dateTimeProvider.Now;
            asset.Condition = AssetCondition.Baik;
            _context.Assets.Add(asset);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Aset berhasil ditambahkan");
        }

        public async Task<OperationResult> UpdateAssetAsync(int id, Asset asset, CancellationToken cancellationToken = default)
        {
            if (id != asset.Id)
            {
                return OperationResult.NotFound("Aset tidak ditemukan");
            }

            var existingAsset = await _context.Assets.FindAsync([id], cancellationToken);
            if (existingAsset == null)
            {
                return OperationResult.NotFound("Aset tidak ditemukan");
            }

            existingAsset.Name = asset.Name;
            existingAsset.AssetType = asset.AssetType;
            existingAsset.Quantity = asset.Quantity;
            existingAsset.Unit = asset.Unit;
            existingAsset.Condition = asset.Condition;
            existingAsset.PurchaseDate = asset.PurchaseDate;
            existingAsset.PurchasePrice = asset.PurchasePrice;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Aset berhasil diupdate");
        }
    }
}
