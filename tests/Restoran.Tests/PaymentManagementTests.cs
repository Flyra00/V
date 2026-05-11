using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Admin.Services;
using Restoran.Features.Kasir.Dtos;
using Restoran.Features.Kasir.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Tests;

public static class PaymentManagementTests
{
    public static async Task Migration_ApplyPaymentSchema_ToSqlite()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"restoran-payment-final-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var context = new ApplicationDbContext(options))
            {
                await context.Database.MigrateAsync();

                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                TestAssert.True(appliedMigrations.Any(migration => migration.Contains("RemoveTransactionPaymentLegacyFields", StringComparison.Ordinal)));
            }

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('Payments', 'PaymentMethodOptions')
            """;

            var paymentTableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            TestAssert.Equal(2, paymentTableCount);
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

    public static async Task CreateOrderAsync_CreatesPaymentRecord()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Makanan" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Paket Sarapan", CategoryId = 1, Price = 30000m, IsAvailable = true });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "1", Capacity = 4, Status = TableStatus.Available });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
        }

        await using var context = database.CreateContext();
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("TRX-PAY-001"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 5m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            TableId = 1,
            CustomerName = "Guest Payment",
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

        var transaction = await context.Transactions.Include(entity => entity.Payment).SingleAsync();
        var payment = await context.Payments.Include(entity => entity.PaymentMethodOption).SingleAsync();
        TestAssert.Equal(transaction.Total, payment.Amount);
        TestAssert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
        TestAssert.Equal(PaymentMethod.QRIS, payment.PaymentMethodOption.LegacyMethod);
    }

    public static async Task CreatePosOrderAsync_CreatesPaidCashPayment()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 11, 11, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Minuman" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Es Kopi", CategoryId = 1, Price = 18000m, IsAvailable = true });
            arrangeContext.Users.Add(new User
            {
                Id = 99,
                Username = "kasir-test",
                Email = "kasir-test@local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Kasir,
                IsActive = true
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("POS-PAY-001"),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 0m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var result = await service.CreatePosOrderAsync(
            new CreatePosOrderRequest
            {
                CustomerName = "Walk-in",
                PaymentMethod = PaymentMethod.Tunai,
                Items =
                [
                    new PosOrderItemRequest
                    {
                        ProductId = 1,
                        Quantity = 2
                    }
                ]
            },
            userId: 99);

        TestAssert.True(result.Succeeded);

        var transaction = await context.Transactions.Include(entity => entity.Payment).SingleAsync();
        var payment = await context.Payments.Include(entity => entity.PaymentMethodOption).SingleAsync();
        TestAssert.NotNull(transaction.Payment);
        TestAssert.Equal(PaymentStatus.Paid, transaction.Payment!.PaymentStatus);
        TestAssert.Equal(now, transaction.Payment.PaymentDate);
        TestAssert.Equal(PaymentStatus.Paid, payment.PaymentStatus);
        TestAssert.Equal(now, payment.PaymentDate);
        TestAssert.Equal("Tunai", payment.PaymentMethodOption.DisplayName);
    }

    public static async Task AdminService_UpdatePaymentMethodAsync_PersistsChanges()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using (var arrangeContext = database.CreateContext())
        {
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
        }

        await using var context = database.CreateContext();
        var service = new AdminService(context, new FixedDateTimeProvider(new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc)));
        var existing = await context.PaymentMethodOptions.SingleAsync(option => option.Code == "transfer");

        var result = await service.UpdatePaymentMethodAsync(existing.Id, new PaymentMethodFormViewModel
        {
            Id = existing.Id,
            Code = "transfer-bank",
            DisplayName = "Transfer Bank",
            LegacyMethod = PaymentMethod.Transfer,
            IsActive = true,
            IsCustomerFacing = true,
            IsCashierFacing = true,
            SortOrder = 5
        });

        TestAssert.True(result.Succeeded);

        var updated = await context.PaymentMethodOptions.SingleAsync(option => option.Id == existing.Id);
        TestAssert.Equal("transfer-bank", updated.Code);
        TestAssert.Equal("Transfer Bank", updated.DisplayName);
        TestAssert.Equal(5, updated.SortOrder);
    }
}
