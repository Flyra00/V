using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Models;

namespace Restoran.Features.Customer.Services
{
    public class CustomerMenuService : ICustomerMenuService
    {
        private readonly ApplicationDbContext _context;

        public CustomerMenuService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Product>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }
    }
}
