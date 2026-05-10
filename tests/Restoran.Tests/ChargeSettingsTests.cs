using Restoran.Features.Admin.Services;
using Restoran.Infrastructure.Persistence;
using Restoran.ViewModels;

namespace Restoran.Tests;

public static class ChargeSettingsTests
{
    public static async Task AdminService_UpdateChargeSettingsAsync_PersistsChargeConfiguration()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new AdminService(context, new FixedDateTimeProvider(DateTime.UtcNow));

        var result = await service.UpdateChargeSettingsAsync(new ChargeSettingsViewModel
        {
            TaxName = "PPN Restoran",
            TaxPercentage = 11m,
            IsTaxActive = true,
            ServiceChargeName = "Service Dining",
            ServiceChargePercentage = 7.5m,
            IsServiceChargeActive = true
        });

        TestAssert.True(result.Succeeded);
        TestAssert.Equal(1, context.TaxSettings.Count());
        TestAssert.Equal(1, context.ServiceChargeSettings.Count());
        TestAssert.Equal(11m, context.TaxSettings.Single().Percentage);
        TestAssert.Equal(7.5m, context.ServiceChargeSettings.Single().Percentage);
    }

    public static async Task ChargeConfigurationProvider_GetCurrentAsync_UsesActiveDatabaseSettings()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.TaxSettings.Add(new Restoran.Models.TaxSetting
            {
                Name = "PPN 11",
                Percentage = 11m,
                IsActive = true
            });
            arrangeContext.ServiceChargeSettings.Add(new Restoran.Models.ServiceChargeSetting
            {
                Name = "Service 6",
                Percentage = 6m,
                IsActive = true
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var provider = new ChargeConfigurationProvider(context, TestOptions.CreateAppSettings());

        var configuration = await provider.GetCurrentAsync();

        TestAssert.Equal("PPN 11", configuration.TaxName);
        TestAssert.Equal(11m, configuration.TaxRate);
        TestAssert.Equal("Service 6", configuration.ServiceChargeName);
        TestAssert.Equal(6m, configuration.ServiceChargeRate);
        TestAssert.Equal(11000m, configuration.CalculateTax(100000m));
        TestAssert.Equal(6000m, configuration.CalculateServiceCharge(100000m));
    }
}
