namespace Restoran.Shared.Abstractions
{
    public interface ITransactionNumberGenerator
    {
        Task<string> GenerateAsync(CancellationToken cancellationToken = default);
    }
}
