using Microsoft.AspNetCore.Http;

namespace Restoran.Shared.Abstractions
{
    public interface IPaymentProofStorage
    {
        Task<string> SaveAsync(string transactionNumber, IFormFile file, CancellationToken cancellationToken = default);
    }
}
