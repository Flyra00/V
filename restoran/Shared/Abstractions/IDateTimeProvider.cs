namespace Restoran.Shared.Abstractions
{
    public interface IDateTimeProvider
    {
        DateTime Now { get; }
    }
}
