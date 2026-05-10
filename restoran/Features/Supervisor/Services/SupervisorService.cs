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

        public async Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Ingredients
                .AsNoTracking()
                .OrderBy(i => i.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Ingredient?> GetIngredientByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Ingredients.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreateIngredientAsync(Ingredient ingredient, CancellationToken cancellationToken = default)
        {
            ingredient.CreatedAt = _dateTimeProvider.Now;
            _context.Ingredients.Add(ingredient);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Bahan baku berhasil ditambahkan");
        }

        public async Task<OperationResult> UpdateIngredientAsync(int id, Ingredient ingredient, CancellationToken cancellationToken = default)
        {
            if (id != ingredient.Id)
            {
                return OperationResult.NotFound("Bahan baku tidak ditemukan");
            }

            var existingIngredient = await _context.Ingredients.FindAsync([id], cancellationToken);
            if (existingIngredient == null)
            {
                return OperationResult.NotFound("Bahan baku tidak ditemukan");
            }

            existingIngredient.Name = ingredient.Name;
            existingIngredient.Unit = ingredient.Unit;
            existingIngredient.StockQuantity = ingredient.StockQuantity;
            existingIngredient.MinStock = ingredient.MinStock;
            existingIngredient.Supplier = ingredient.Supplier;
            existingIngredient.LastUpdated = _dateTimeProvider.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Bahan baku berhasil diupdate");
        }

        public async Task<OperationResult<decimal>> AddStockAsync(int id, decimal amount, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
            {
                return OperationResult<decimal>.Failure("Jumlah stok harus lebih dari 0");
            }

            var ingredient = await _context.Ingredients.FindAsync([id], cancellationToken);
            if (ingredient == null)
            {
                return OperationResult<decimal>.NotFound("Bahan baku tidak ditemukan");
            }

            ingredient.StockQuantity += amount;
            ingredient.LastUpdated = _dateTimeProvider.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return OperationResult<decimal>.Success(ingredient.StockQuantity);
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
