using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Restoran.Shared.Abstractions;

namespace Restoran.Infrastructure.Files
{
    public class PaymentProofStorage : IPaymentProofStorage
    {
        private readonly IWebHostEnvironment _environment;

        public PaymentProofStorage(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> SaveAsync(string transactionNumber, IFormFile file, CancellationToken cancellationToken = default)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "payments");
            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{transactionNumber}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            return $"/uploads/payments/{uniqueFileName}";
        }
    }
}
