using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using Restoran.Controllers;
using Restoran.Data;
using Restoran.Features.Admin.Services;
using Restoran.Features.Kasir.Dtos;
using Restoran.Features.Kasir.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Payments.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.Shared.Results;
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
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            TableId = 1,
            CustomerName = "Guest Payment",
            PaymentMethod = PaymentMethod.Tunai,
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
        TestAssert.Equal(PaymentMethod.Tunai, payment.PaymentMethodOption.LegacyMethod);
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
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

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

    public static async Task ConfirmPaymentAsync_Cash_SetsAmountReceivedAndChange()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Promos.Add(new Promo
            {
                Id = 3,
                Name = "Promo Aktif",
                PromoType = PromoType.Percentage,
                DiscountValue = 10m,
                MinimumPurchase = 0m,
                IsActive = true,
                StartsAt = now.AddDays(-1),
                EndsAt = now.AddDays(1)
            });
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-CASH-001",
                CustomerName = "Cash Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 100000m);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.ConfirmPaymentAsync(1, 150000m);

        TestAssert.True(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Paid, payment.PaymentStatus);
        TestAssert.Equal(150000m, payment.AmountReceived);
        TestAssert.Equal(50000m, payment.ChangeAmount);
    }

    public static async Task ConfirmPaymentAsync_Cash_RejectsInsufficientAmount()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-CASH-002",
                CustomerName = "Cash Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 100000m);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(DateTime.UtcNow),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(DateTime.UtcNow)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.ConfirmPaymentAsync(1, 50000m);

        TestAssert.False(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
    }

    public static async Task ApplyPromoAsync_AllowsManualApply_IgnoresMinimumPurchase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 13, 9, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-PROMO-001",
                CustomerName = "Promo Guest",
                Subtotal = 100000m,
                Tax = 10000m,
                ServiceCharge = 0m,
                Discount = 0m,
                Total = 110000m,
                OrderStatus = OrderStatus.Served
            });
            arrangeContext.Promos.Add(new Promo
            {
                Id = 1,
                Name = "Promo Kasir 10%",
                PromoType = PromoType.Percentage,
                DiscountValue = 10m,
                MinimumPurchase = 1000000m,
                IsActive = true,
                StartsAt = now.AddDays(-1),
                EndsAt = now.AddDays(2)
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 110000m);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 0m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.ApplyPromoAsync(1, 1);

        TestAssert.True(result.Succeeded);
        var transaction = await context.Transactions.SingleAsync();
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(10000m, transaction.Discount);
        TestAssert.Equal(100000m, transaction.Total);
        TestAssert.Equal("Promo Kasir 10%", transaction.AppliedPromoName);
        TestAssert.Equal(100000m, payment.Amount);
    }

    public static async Task ApplyPromoAsync_FailsForPaidTransaction()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-PROMO-002",
                CustomerName = "Promo Guest",
                Subtotal = 100000m,
                Tax = 10000m,
                ServiceCharge = 0m,
                Discount = 0m,
                Total = 110000m,
                OrderStatus = OrderStatus.Served
            });
            arrangeContext.Promos.Add(new Promo
            {
                Id = 2,
                Name = "Promo Kasir",
                PromoType = PromoType.Percentage,
                DiscountValue = 10m,
                MinimumPurchase = 0m,
                IsActive = true,
                StartsAt = now.AddDays(-1),
                EndsAt = now.AddDays(2)
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Paid, 110000m, now);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 0m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.ApplyPromoAsync(1, 2);

        TestAssert.False(result.Succeeded);
    }

    public static async Task RemovePromoAsync_SucceedsWhenPending()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 13, 11, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-PROMO-003",
                CustomerName = "Promo Guest",
                Subtotal = 100000m,
                Tax = 10000m,
                ServiceCharge = 0m,
                Discount = 10000m,
                AppliedPromoName = "Promo Aktif",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 100000m);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 0m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.RemovePromoAsync(1);

        TestAssert.True(result.Succeeded);
        var transaction = await context.Transactions.SingleAsync();
        TestAssert.Equal(0m, transaction.Discount);
        TestAssert.Equal(string.Empty, transaction.AppliedPromoName);
        TestAssert.Equal(null, transaction.PromoId);
        TestAssert.Equal(110000m, transaction.Total);
    }

    public static async Task ConfirmPaymentAsync_AfterPromo_UsesUpdatedTotal()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-PROMO-004",
                CustomerName = "Promo Guest",
                Subtotal = 100000m,
                Tax = 10000m,
                ServiceCharge = 0m,
                Discount = 10000m,
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 100000m);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 0m),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.ConfirmPaymentAsync(1, 100000m);

        TestAssert.True(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Paid, payment.PaymentStatus);
        TestAssert.Equal(100000m, payment.AmountReceived);
        TestAssert.Equal(0m, payment.ChangeAmount);
    }

    public static async Task PrintReceipt_PaidOnlyGuard_Works()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 13, 13, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Users.Add(new User
            {
                Id = 99,
                Username = "kasir-receipt",
                Email = "kasir-receipt@local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Kasir,
                IsActive = true
            });

            arrangeContext.Transactions.AddRange(
                new Transaction
                {
                    Id = 1,
                    TransactionNumber = "TRX-REC-001",
                    CustomerName = "Guest Pending",
                    UserId = 99,
                    Subtotal = 50000m,
                    Tax = 5000m,
                    ServiceCharge = 0m,
                    Discount = 0m,
                    Total = 55000m,
                    OrderStatus = OrderStatus.Served
                },
                new Transaction
                {
                    Id = 2,
                    TransactionNumber = "TRX-REC-002",
                    CustomerName = "Guest Paid",
                    UserId = 99,
                    Subtotal = 50000m,
                    Tax = 5000m,
                    ServiceCharge = 0m,
                    Discount = 0m,
                    Total = 55000m,
                    OrderStatus = OrderStatus.Served
                });

            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 55000m);
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 2, PaymentMethod.Tunai, PaymentStatus.Paid, 55000m, now);
        }

        await using var context = database.CreateContext();
        var cashierService = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());
        var controller = new KasirController(cashierService, new StubAuthCookieService());

        var pendingResult = await controller.PrintReceipt(1);
        TestAssert.True(pendingResult is RedirectToActionResult);

        var paidResult = await controller.PrintReceipt(2);
        var paidView = paidResult as ViewResult;
        TestAssert.NotNull(paidView);
        TestAssert.Equal("Receipt", paidView!.ViewName);
    }

    public static async Task ConfirmPaymentAsync_RejectsOnlineManualConfirmation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-001",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.QRIS, PaymentStatus.Pending, 100000m);
        }

        await using var context = database.CreateContext();
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(DateTime.UtcNow),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(DateTime.UtcNow)),
            TestPaymentData.CreatePaymentService(context),
            new StubMidtransService());

        var result = await service.ConfirmPaymentAsync(1, null);

        TestAssert.False(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
    }

    public static async Task CheckMidtransStatusAsync_Settlement_MarksPaid()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-002",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-ORDER-001";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var midtrans = new StubMidtransService(applyStatus: async (transactionId, _) =>
        {
            var transaction = await context.Transactions.Include(entity => entity.Payment).SingleAsync(entity => entity.Id == transactionId);
            transaction.Payment!.PaymentStatus = PaymentStatus.Paid;
            transaction.Payment.PaymentDate = now;
            transaction.Payment.Amount = transaction.Total;
            transaction.Payment.AmountReceived = transaction.Total;
            transaction.Payment.ChangeAmount = 0;
            transaction.Payment.MidtransTransactionStatus = "settlement";
            await context.SaveChangesAsync();
            return OperationResult.Success("Pembayaran Midtrans sudah lunas.");
        });
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context),
            midtrans);

        var result = await service.CheckMidtransStatusAsync(1);

        TestAssert.True(result.Succeeded);
        var savedPayment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Paid, savedPayment.PaymentStatus);
        TestAssert.Equal(100000m, savedPayment.AmountReceived);
        TestAssert.Equal(0m, savedPayment.ChangeAmount);
    }

    public static async Task CheckMidtransStatusAsync_Pending_KeepsPending()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-003",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-ORDER-002";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var midtrans = new StubMidtransService(applyStatus: async (transactionId, _) =>
        {
            var transaction = await context.Transactions.Include(entity => entity.Payment).SingleAsync(entity => entity.Id == transactionId);
            transaction.Payment!.PaymentStatus = PaymentStatus.Pending;
            transaction.Payment.MidtransTransactionStatus = "pending";
            await context.SaveChangesAsync();
            return OperationResult.Success("Pembayaran Midtrans masih menunggu.");
        });
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(DateTime.UtcNow),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(DateTime.UtcNow)),
            TestPaymentData.CreatePaymentService(context),
            midtrans);

        var result = await service.CheckMidtransStatusAsync(1);

        TestAssert.True(result.Succeeded);
        var savedPayment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Pending, savedPayment.PaymentStatus);
    }

    public static async Task CheckMidtransStatusAsync_NotFound_KeepsPending()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-004",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-ORDER-003";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var midtrans = new StubMidtransService(applyStatus: (_, _) =>
            Task.FromResult(OperationResult.Failure("Transaksi belum dipilih/dibayar di halaman Midtrans.")));
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(DateTime.UtcNow),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(DateTime.UtcNow)),
            TestPaymentData.CreatePaymentService(context),
            midtrans);

        var result = await service.CheckMidtransStatusAsync(1);

        TestAssert.False(result.Succeeded);
        var savedPayment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Pending, savedPayment.PaymentStatus);
    }

    public static async Task MidtransService_ExpireStatus_MarksPaymentCancelled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-EXPIRE-001",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-ORDER-EXPIRE-001";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = MidtransTestFactory.CreateService(
            context,
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "order_id": "MID-ORDER-EXPIRE-001",
                  "transaction_id": "midtrans-tx-001",
                  "transaction_status": "expire",
                  "payment_type": "bank_transfer",
                  "gross_amount": "100000.00",
                  "status_code": "407",
                  "status_message": "Payment expired."
                }
                """)
            }));

        var result = await service.ApplyStatusToPaymentAsync(1);

        TestAssert.True(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Cancelled, payment.PaymentStatus);
        TestAssert.Equal("expire", payment.MidtransTransactionStatus);
    }

    public static async Task MidtransService_ProcessNotificationAsync_ValidSignature_MarksPaid()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        const string serverKey = "SB-Mid-server-test";

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-WEBHOOK-001",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.QRIS, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-WEBHOOK-001";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = MidtransTestFactory.CreateService(
            context,
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }),
            serverKey);
        var notification = new MidtransNotificationRequest
        {
            OrderId = "MID-WEBHOOK-001",
            TransactionId = "midtrans-notif-001",
            TransactionStatus = "settlement",
            PaymentType = "qris",
            GrossAmount = "100000.00",
            StatusCode = "200",
            StatusMessage = "midtrans payment notification",
            FraudStatus = "accept",
            SignatureKey = MidtransTestFactory.CreateNotificationSignature("MID-WEBHOOK-001", "200", "100000.00", serverKey)
        };

        var result = await service.ProcessNotificationAsync(notification);

        TestAssert.True(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Paid, payment.PaymentStatus);
        TestAssert.Equal("settlement", payment.MidtransTransactionStatus);
        TestAssert.Equal("midtrans-notif-001", payment.MidtransTransactionId);
    }

    public static async Task MidtransService_ProcessNotificationAsync_InvalidSignature_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        const string serverKey = "SB-Mid-server-test";

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-WEBHOOK-002",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-WEBHOOK-002";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = MidtransTestFactory.CreateService(
            context,
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }),
            serverKey);

        var result = await service.ProcessNotificationAsync(new MidtransNotificationRequest
        {
            OrderId = "MID-WEBHOOK-002",
            TransactionId = "midtrans-notif-002",
            TransactionStatus = "settlement",
            PaymentType = "bank_transfer",
            GrossAmount = "100000.00",
            StatusCode = "200",
            StatusMessage = "midtrans payment notification",
            FraudStatus = "accept",
            SignatureKey = "invalid-signature"
        });

        TestAssert.False(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
    }

    public static async Task MidtransService_ProcessNotificationAsync_MismatchedGrossAmount_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        const string serverKey = "SB-Mid-server-test";

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-WEBHOOK-003",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 100000m);
            seededPayment.MidtransOrderId = "MID-WEBHOOK-003";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = MidtransTestFactory.CreateService(
            context,
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }),
            serverKey);

        var result = await service.ProcessNotificationAsync(new MidtransNotificationRequest
        {
            OrderId = "MID-WEBHOOK-003",
            TransactionId = "midtrans-notif-003",
            TransactionStatus = "settlement",
            PaymentType = "bank_transfer",
            GrossAmount = "99999.00",
            StatusCode = "200",
            StatusMessage = "midtrans payment notification",
            FraudStatus = "accept",
            SignatureKey = MidtransTestFactory.CreateNotificationSignature("MID-WEBHOOK-003", "200", "99999.00", serverKey)
        });

        TestAssert.False(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
    }

    public static async Task MidtransService_StalePendingStatus_DoesNotDowngradePaidPayment()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var paidAt = new DateTime(2026, 5, 12, 12, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-MID-STALE-001",
                CustomerName = "Midtrans Guest",
                Total = 100000m,
                OrderStatus = OrderStatus.Served
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            var seededPayment = await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.QRIS, PaymentStatus.Paid, 100000m, paidAt);
            seededPayment.MidtransOrderId = "MID-STALE-001";
            seededPayment.AmountReceived = 100000m;
            seededPayment.ChangeAmount = 0m;
            seededPayment.MidtransTransactionStatus = "settlement";
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = MidtransTestFactory.CreateService(
            context,
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "order_id": "MID-STALE-001",
                  "transaction_id": "midtrans-stale-001",
                  "transaction_status": "pending",
                  "payment_type": "qris",
                  "gross_amount": "100000.00",
                  "status_code": "201",
                  "status_message": "Transaction is pending."
                }
                """)
            }));

        var result = await service.ApplyStatusToPaymentAsync(1);

        TestAssert.True(result.Succeeded);
        var payment = await context.Payments.SingleAsync();
        TestAssert.Equal(PaymentStatus.Paid, payment.PaymentStatus);
        TestAssert.Equal(paidAt, payment.PaymentDate);
        TestAssert.Equal("pending", payment.MidtransTransactionStatus);
    }

    public static async Task MidtransService_CreateSnapTransactionAsync_IncludesProviderErrorMessage()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = MidtransTestFactory.CreateService(
            context,
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""
                {
                  "status_code": "401",
                  "status_message": "Transaction cannot be authorized with the current client/server key."
                }
                """)
            }));

        var response = await service.CreateSnapTransactionAsync(new Transaction
        {
            TransactionNumber = "TRX-MID-ERR-001",
            CustomerName = "Guest",
            Total = 100000m,
            Payment = new Payment
            {
                MidtransOrderId = "MID-ERR-001"
            }
        });

        TestAssert.False(response.Succeeded);
        TestAssert.True(response.Message.Contains("401", StringComparison.Ordinal));
        TestAssert.True(response.Message.Contains("current client/server key", StringComparison.OrdinalIgnoreCase));
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
