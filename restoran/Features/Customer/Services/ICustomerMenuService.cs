using Restoran.Models;

namespace Restoran.Features.Customer.Services
{
    public interface ICustomerMenuService
    {
        Task<IReadOnlyList<Product>> GetAvailableProductsAsync(CancellationToken cancellationToken = default);
    }
}
