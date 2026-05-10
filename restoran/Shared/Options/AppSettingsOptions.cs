namespace Restoran.Shared.Options
{
    public class AppSettingsOptions
    {
        public string CompanyName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; } = 0.10m;
        public decimal ServiceChargeRate { get; set; } = 0.05m;
        public int PointsPerTransaction { get; set; }
    }
}
