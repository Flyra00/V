using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Shared.Abstractions;

namespace Restoran.Infrastructure.Persistence
{
    public class TransactionNumberGenerator : ITransactionNumberGenerator
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public TransactionNumberGenerator(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            var dateSegment = _dateTimeProvider.Now.ToString("yyyyMMdd");
            var count = await _context.Transactions
                .AsNoTracking()
                .CountAsync(t => t.TransactionNumber.StartsWith($"TRX-{dateSegment}"), cancellationToken);

            return $"TRX-{dateSegment}-{(count + 1):D4}";
        }
    }
}
