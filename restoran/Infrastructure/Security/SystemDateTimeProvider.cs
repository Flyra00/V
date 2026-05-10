using Restoran.Shared.Abstractions;

namespace Restoran.Infrastructure.Security
{
    public class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
    }
}
