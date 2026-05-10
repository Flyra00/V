namespace Restoran.Shared.Abstractions
{
    public interface IChargeConfigurationProvider
    {
        Task<ChargeConfiguration> GetCurrentAsync(CancellationToken cancellationToken = default);
    }

    public sealed class ChargeConfiguration
    {
        public string TaxName { get; init; } = string.Empty;
        public decimal TaxRate { get; init; }
        public bool IsTaxActive { get; init; }
        public string ServiceChargeName { get; init; } = string.Empty;
        public decimal ServiceChargeRate { get; init; }
        public bool IsServiceChargeActive { get; init; }

        public decimal CalculateTax(decimal subtotal)
            => IsTaxActive && TaxRate > 0 ? subtotal * (TaxRate / 100m) : 0m;

        public decimal CalculateServiceCharge(decimal subtotal)
            => IsServiceChargeActive && ServiceChargeRate > 0 ? subtotal * (ServiceChargeRate / 100m) : 0m;
    }
}
