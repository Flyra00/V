namespace Restoran.Features.Supervisor.Dtos
{
    public class AssetLookupItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
    }
}
