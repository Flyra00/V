using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restoran.Data;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Options;

namespace Restoran.Infrastructure.Persistence
{
    public class ChargeConfigurationProvider : IChargeConfigurationProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly AppSettingsOptions _appSettings;

        public ChargeConfigurationProvider(ApplicationDbContext context, IOptions<AppSettingsOptions> appSettings)
        {
            _context = context;
            _appSettings = appSettings.Value;
        }

        public async Task<ChargeConfiguration> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            var activeTax = await _context.TaxSettings
                .AsNoTracking()
                .OrderByDescending(setting => setting.IsActive)
                .ThenBy(setting => setting.Id)
                .FirstOrDefaultAsync(setting => setting.IsActive, cancellationToken);

            var activeServiceCharge = await _context.ServiceChargeSettings
                .AsNoTracking()
                .OrderByDescending(setting => setting.IsActive)
                .ThenBy(setting => setting.Id)
                .FirstOrDefaultAsync(setting => setting.IsActive, cancellationToken);

            var fallbackTaxRate = _appSettings.TaxRate > 0 ? _appSettings.TaxRate * 100m : 0m;
            var fallbackServiceRate = _appSettings.ServiceChargeRate > 0 ? _appSettings.ServiceChargeRate * 100m : 0m;

            return new ChargeConfiguration
            {
                TaxName = activeTax?.Name ?? "PPN",
                TaxRate = activeTax?.Percentage ?? fallbackTaxRate,
                IsTaxActive = activeTax?.IsActive ?? fallbackTaxRate > 0,
                ServiceChargeName = activeServiceCharge?.Name ?? "Service Charge",
                ServiceChargeRate = activeServiceCharge?.Percentage ?? fallbackServiceRate,
                IsServiceChargeActive = activeServiceCharge?.IsActive ?? fallbackServiceRate > 0
            };
        }
    }
}
