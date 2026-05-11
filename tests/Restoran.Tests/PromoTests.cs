using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Restoran.Data;
using Restoran.Features.Admin.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Tests;

public static class PromoTests
{
    public static async Task Migration_ApplyPromoSchema_ToSqlite()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"restoran-promo-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var context = new ApplicationDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM sqlite_master
                    WHERE type = 'table'
                      AND name = 'Promos'
                """;

                var promoTableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                TestAssert.Equal(1, promoTableCount);

                command.CommandText = """
                    SELECT COUNT(*)
                    FROM pragma_table_info('Transactions')
                    WHERE name IN ('PromoId', 'AppliedPromoName')
                """;

                var transactionPromoColumns = Convert.ToInt32(await command.ExecuteScalarAsync());
                TestAssert.Equal(2, transactionPromoColumns);
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    public static async Task CreateOrderAsync_AppliesBestEligiblePromo()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Makanan" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Ayam Bakar", CategoryId = 1, Price = 50000m, IsAvailable = true });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "1", Capacity = 4, Status = TableStatus.Available });
            arrangeContext.Promos.AddRange(
                new Promo
                {
                    Id = 1,
                    Name = "HEMAT10",
                    DiscountValue = 10m,
                    MinimumPurchase = 30000m,
                    StartsAt = now.AddDays(-1),
                    EndsAt = now.AddDays(1),
                    IsActive = true
                },
                new Promo
                {
                    Id = 2,
                    Name = "HEMAT20",
                    DiscountValue = 20m,
                    MinimumPurchase = 80000m,
                    StartsAt = now.AddDays(-1),
                    EndsAt = now.AddDays(1),
                    IsActive = true
                });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("TRX-PROMO-01"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 5m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            TableId = 1,
            CustomerName = "Promo Guest",
            PaymentMethod = PaymentMethod.BayarDiKasir,
            Items =
            [
                new OrderItemRequest
                {
                    ProductId = 1,
                    Quantity = 2
                }
            ]
        });

        TestAssert.True(result.Succeeded);
        var transaction = await context.Transactions.SingleAsync();
        TestAssert.Equal("HEMAT20", transaction.AppliedPromoName);
        TestAssert.Equal(20000m, transaction.Discount);
        TestAssert.Equal(95000m, transaction.Total);
    }

    public static async Task CreateOrderAsync_StacksMemberAndPromoDiscount()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Minuman" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Paket Teh", CategoryId = 1, Price = 50000m, IsAvailable = true });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "2", Capacity = 4, Status = TableStatus.Available });
            arrangeContext.Users.Add(new User
            {
                Id = 1,
                Username = "member-gold",
                Email = "member-gold@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Member,
                IsActive = true
            });
            arrangeContext.Members.Add(new Member
            {
                Id = 1,
                UserId = 1,
                FullName = "Member Gold",
                MemberType = MemberType.Gold
            });
            arrangeContext.Promos.Add(new Promo
            {
                Id = 1,
                Name = "LUNCH20",
                DiscountValue = 20m,
                MinimumPurchase = 30000m,
                StartsAt = now.AddDays(-1),
                EndsAt = now.AddDays(1),
                IsActive = true
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("TRX-PROMO-02"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 5m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            TableId = 1,
            CustomerName = "Member Gold",
            IsMember = true,
            MemberId = 1,
            PaymentMethod = PaymentMethod.QRIS,
            Items =
            [
                new OrderItemRequest
                {
                    ProductId = 1,
                    Quantity = 2
                }
            ]
        });

        TestAssert.True(result.Succeeded);
        var transaction = await context.Transactions.SingleAsync();
        TestAssert.Equal("LUNCH20", transaction.AppliedPromoName);
        TestAssert.Equal(28000m, transaction.Discount);
        TestAssert.Equal(87000m, transaction.Total);
    }

    public static async Task AdminService_CreatePromoAsync_RejectsInvalidPeriod()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new AdminService(context, new FixedDateTimeProvider(new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc)));

        var result = await service.CreatePromoAsync(new PromoFormViewModel
        {
            Name = "FLASH",
            DiscountValue = 15m,
            MinimumPurchase = 10000m,
            StartsAt = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        TestAssert.False(result.Succeeded);
        TestAssert.Equal(0, await context.Promos.CountAsync());
    }

    public static async Task AdminService_UpdatePromoAsync_PersistsChanges()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Promos.Add(new Promo
            {
                Id = 1,
                Name = "EARLY10",
                DiscountValue = 10m,
                MinimumPurchase = 50000m,
                StartsAt = now,
                EndsAt = now.AddDays(2),
                IsActive = true
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AdminService(context, new FixedDateTimeProvider(now));

        var result = await service.UpdatePromoAsync(1, new PromoFormViewModel
        {
            Id = 1,
            Name = "EARLY15",
            DiscountValue = 15m,
            MinimumPurchase = 75000m,
            StartsAt = now.AddHours(1),
            EndsAt = now.AddDays(3),
            IsActive = false
        });

        TestAssert.True(result.Succeeded);
        var promo = await context.Promos.SingleAsync();
        TestAssert.Equal("EARLY15", promo.Name);
        TestAssert.Equal(15m, promo.DiscountValue);
        TestAssert.Equal(75000m, promo.MinimumPurchase);
        TestAssert.False(promo.IsActive);
    }
}
