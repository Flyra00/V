using Restoran.Features.Dashboard.Services;
using Restoran.Models;

namespace Restoran.Tests;

public static class DashboardInventoryTests
{
    public static async Task Schema_DoesNotCreateIngredientTables_AfterCleanup()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        static async Task<int> CountTableAsync(SqliteTestDatabase db, string tableName)
        {
            await using var command = db.Connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = $tableName
            """;
            command.Parameters.AddWithValue("$tableName", tableName);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        TestAssert.Equal(0, await CountTableAsync(database, "Ingredients"));
        TestAssert.Equal(0, await CountTableAsync(database, "ProductIngredients"));
    }

    public static async Task GetStockReportAsync_ReturnsAssetsAndProductsOnly()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category
            {
                Id = 1,
                Name = "Minuman",
                Description = "Menu minuman"
            });
            arrangeContext.Products.Add(new Product
            {
                Id = 1,
                Name = "Es Teh",
                CategoryId = 1,
                Price = 8000m,
                IsAvailable = true
            });
            arrangeContext.Assets.Add(new Asset
            {
                Id = 1,
                Name = "Gelas Kaca",
                AssetType = AssetType.PeralatanMinum,
                Quantity = 4,
                Unit = "pcs",
                Condition = AssetCondition.RusakRingan
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new DashboardService(
            context,
            new FixedDateTimeProvider(new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc)),
            new StubChargeConfigurationProvider());

        var dashboard = await service.GetDashboardAsync();
        var report = await service.GetStockReportAsync();

        TestAssert.Equal(1, dashboard.InventoryAlerts.Count);
        TestAssert.Equal("Gelas Kaca", dashboard.InventoryAlerts[0].Name);
        TestAssert.Equal("RusakRingan", dashboard.InventoryAlerts[0].Condition);

        TestAssert.Equal(1, report.Assets.Count);
        TestAssert.Equal("Gelas Kaca", report.Assets[0].Name);
        TestAssert.Equal(1, report.ProductsAvailability.Count);
        TestAssert.Equal("Es Teh", report.ProductsAvailability[0].Name);
    }
}
