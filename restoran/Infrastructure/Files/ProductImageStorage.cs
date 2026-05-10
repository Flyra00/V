using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Restoran.Shared.Abstractions;

namespace Restoran.Infrastructure.Files
{
    public class ProductImageStorage : IProductImageStorage
    {
        private readonly IWebHostEnvironment _environment;

        public ProductImageStorage(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            return $"/uploads/products/{fileName}";
        }
    }
}
