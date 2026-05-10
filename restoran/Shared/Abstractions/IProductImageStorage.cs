using Microsoft.AspNetCore.Http;

namespace Restoran.Shared.Abstractions
{
    public interface IProductImageStorage
    {
        Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
    }
}
