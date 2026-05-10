using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;

namespace Restoran.Features.Catalog.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CategoryService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Category?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Categories.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreateCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            if (await _context.Categories.AnyAsync(c => c.Name.ToLower() == category.Name.ToLower(), cancellationToken))
            {
                return OperationResult.Failure("Nama kategori sudah digunakan");
            }

            category.CreatedAt = _dateTimeProvider.Now;
            _context.Categories.Add(category);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Kategori berhasil ditambahkan");
        }

        public async Task<OperationResult> UpdateCategoryAsync(int id, Category category, CancellationToken cancellationToken = default)
        {
            if (id != category.Id)
            {
                return OperationResult.NotFound("Kategori tidak ditemukan");
            }

            var existingCategory = await _context.Categories.FindAsync([id], cancellationToken);
            if (existingCategory == null)
            {
                return OperationResult.NotFound("Kategori tidak ditemukan");
            }

            if (await _context.Categories.AnyAsync(
                    c => c.Name.ToLower() == category.Name.ToLower() && c.Id != category.Id,
                    cancellationToken))
            {
                return OperationResult.Failure("Nama kategori sudah digunakan");
            }

            existingCategory.Name = category.Name;
            existingCategory.Description = category.Description;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Kategori berhasil diupdate");
        }

        public async Task<OperationResult> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (category == null)
            {
                return OperationResult.NotFound("Kategori tidak ditemukan");
            }

            if (category.Products.Any())
            {
                return OperationResult.Failure("Tidak dapat menghapus kategori yang memiliki produk");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Kategori berhasil dihapus");
        }
    }
}
