using Microsoft.EntityFrameworkCore;
using Restoran.Features.Kitchen.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;

namespace Restoran.Tests;

public static class CustomerTrackingTests
{
    public static async Task ResolveTrackingTransactionIdAsync_PrefersActiveTransaction()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 18, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "1", Capacity = 4, Status = TableStatus.Occupied });
            arrangeContext.Transactions.AddRange(
                new Transaction
                {
                    Id = 1,
                    TransactionNumber = "TRX-TRACK-001",
                    TableId = 1,
                    CustomerName = "Guest A",
                    OrderStatus = OrderStatus.New,
                    CreatedAt = now.AddMinutes(-10)
                },
                new Transaction
                {
                    Id = 2,
                    TransactionNumber = "TRX-TRACK-002",
                    TableId = 1,
                    CustomerName = "Guest B",
                    OrderStatus = OrderStatus.New,
                    CreatedAt = now
                });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.BayarDiKasir, PaymentStatus.Pending, 0m);
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 2, PaymentMethod.BayarDiKasir, PaymentStatus.Pending, 0m);
        }

        await using var context = database.CreateContext();
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var resolvedId = await service.ResolveTrackingTransactionIdAsync(1, 1, null);

        TestAssert.Equal(1, resolvedId);
    }

    public static async Task ResolveTrackingTransactionIdAsync_FallsBackToLatestMemberTransaction()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 18, 15, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Users.Add(new User
            {
                Id = 5,
                Username = "member",
                Email = "member@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Member,
                IsActive = true
            });
            arrangeContext.Members.Add(new Member
            {
                Id = 3,
                UserId = 5,
                FullName = "Member Tracking",
                MemberType = MemberType.Silver
            });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "2", Capacity = 4, Status = TableStatus.Occupied });
            arrangeContext.TableSessions.AddRange(
                new TableSession
                {
                    Id = 10,
                    TableId = 1,
                    MemberId = 3,
                    CustomerType = CustomerType.Member,
                    CustomerName = "Member Tracking",
                    StartTime = now.AddMinutes(-30),
                    Status = TableSessionStatus.Active
                },
                new TableSession
                {
                    Id = 11,
                    TableId = 1,
                    MemberId = 3,
                    CustomerType = CustomerType.Member,
                    CustomerName = "Member Tracking",
                    StartTime = now.AddMinutes(-5),
                    Status = TableSessionStatus.Active
                });
            arrangeContext.Transactions.AddRange(
                new Transaction
                {
                    Id = 7,
                    TransactionNumber = "TRX-MEMBER-001",
                    TableId = 1,
                    TableSessionId = 10,
                    CustomerName = "Member Tracking",
                    OrderStatus = OrderStatus.Served,
                    CreatedAt = now.AddMinutes(-25)
                },
                new Transaction
                {
                    Id = 8,
                    TransactionNumber = "TRX-MEMBER-002",
                    TableId = 1,
                    TableSessionId = 11,
                    CustomerName = "Member Tracking",
                    OrderStatus = OrderStatus.New,
                    CreatedAt = now.AddMinutes(-1)
                });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 7, PaymentMethod.BayarDiKasir, PaymentStatus.Pending, 0m);
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 8, PaymentMethod.BayarDiKasir, PaymentStatus.Pending, 0m);
        }

        await using var context = database.CreateContext();
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var resolvedId = await service.ResolveTrackingTransactionIdAsync(null, null, 5);

        TestAssert.Equal(8, resolvedId);
    }

    public static async Task GetTrackingStatusAsync_ReflectsKitchenStatusUpdates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 19, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Makanan" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Soto Ayam", CategoryId = 1, Price = 25000m, IsAvailable = true });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "3", Capacity = 4, Status = TableStatus.Occupied });
            arrangeContext.TableSessions.Add(new TableSession
            {
                Id = 1,
                TableId = 1,
                CustomerType = CustomerType.Guest,
                CustomerName = "Guest Tracking",
                StartTime = now.AddMinutes(-10),
                Status = TableSessionStatus.Active
            });
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-STATUS-001",
                TableId = 1,
                TableSessionId = 1,
                CustomerName = "Guest Tracking",
                OrderStatus = OrderStatus.New,
                CreatedAt = now.AddMinutes(-8)
            });
            arrangeContext.TransactionDetails.Add(new TransactionDetail
            {
                Id = 1,
                TransactionId = 1,
                ProductId = 1,
                Quantity = 1,
                UnitPrice = 25000m,
                Status = DetailStatus.Pending,
                CreatedAt = now.AddMinutes(-8)
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Transfer, PaymentStatus.Pending, 25000m);
        }

        await using var context = database.CreateContext();
        var tableService = new TableService(context, new FixedDateTimeProvider(now));
        var kitchenService = new KitchenService(context, new FixedDateTimeProvider(now), tableService);
        var orderService = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(),
            tableService,
            TestPaymentData.CreatePaymentService(context));

        var updateResult = await kitchenService.UpdateStatusAsync(1, OrderStatus.Processing);
        TestAssert.True(updateResult.Succeeded);

        var trackingStatus = await orderService.GetTrackingStatusAsync(1);
        TestAssert.NotNull(trackingStatus);
        TestAssert.Equal(OrderStatus.Processing, trackingStatus!.OrderStatus);
        TestAssert.Equal(DetailStatus.Preparing, trackingStatus.Items.Single().Status);
    }

    public static async Task GetTrackingAsync_ReturnsPaymentAndPromoSummary()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 10, 19, 30, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Minuman" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Es Teh", CategoryId = 1, Price = 10000m, IsAvailable = true });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "4", Capacity = 4, Status = TableStatus.Occupied });
            arrangeContext.TableSessions.Add(new TableSession
            {
                Id = 2,
                TableId = 1,
                CustomerType = CustomerType.Guest,
                CustomerName = "Guest Promo",
                StartTime = now.AddMinutes(-6),
                Status = TableSessionStatus.Active
            });
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 2,
                TransactionNumber = "TRX-SUMMARY-001",
                TableId = 1,
                TableSessionId = 2,
                CustomerName = "Guest Promo",
                OrderStatus = OrderStatus.Ready,
                Subtotal = 30000m,
                Discount = 5000m,
                Tax = 3000m,
                ServiceCharge = 1500m,
                Total = 29500m,
                AppliedPromoName = "MALAM15",
                CreatedAt = now.AddMinutes(-5)
            });
            arrangeContext.TransactionDetails.Add(new TransactionDetail
            {
                Id = 2,
                TransactionId = 2,
                ProductId = 1,
                Quantity = 3,
                UnitPrice = 10000m,
                Status = DetailStatus.Ready,
                CreatedAt = now.AddMinutes(-5)
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 2, PaymentMethod.Transfer, PaymentStatus.Pending, 29500m);
        }

        await using var context = database.CreateContext();
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(),
            new TableService(context, new FixedDateTimeProvider(now)),
            TestPaymentData.CreatePaymentService(context));

        var tracking = await service.GetTrackingAsync(2);
        TestAssert.NotNull(tracking);
        TestAssert.Equal("TRX-SUMMARY-001", tracking!.TransactionNumber);
        TestAssert.Equal(PaymentMethod.Transfer, tracking.PaymentMethod);
        TestAssert.Equal("MALAM15", tracking.AppliedPromoName);
        TestAssert.True(tracking.RequiresOnlinePaymentProof);
        TestAssert.Equal(1, tracking.Items.Count);
    }
}
