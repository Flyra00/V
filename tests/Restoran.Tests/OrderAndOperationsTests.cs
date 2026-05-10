using Microsoft.EntityFrameworkCore;
using Restoran.Features.Kasir.Services;
using Restoran.Features.Kitchen.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Supervisor.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Tests;

public static class OrderAndOperationsTests
{
    public static async Task CreateOrderAsync_ComputesTotals_Discount_AndNotification()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Makanan" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Nasi Goreng", CategoryId = 1, Price = 20000, IsAvailable = true });
            arrangeContext.Tables.Add(new Table { Id = 1, TableNumber = "1", Capacity = 4, Status = TableStatus.Available });
            arrangeContext.Users.Add(new User
            {
                Id = 1,
                Username = "member-user",
                Email = "member@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Member,
                IsActive = true
            });
            arrangeContext.Members.Add(new Member
            {
                Id = 1,
                UserId = 1,
                FullName = "Member Test",
                MemberType = MemberType.Gold
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var tableService = new TableService(context, new FixedDateTimeProvider(now));
        var service = new OrderService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("TRX-001"),
            new StubPaymentProofStorage(),
            new StubChargeConfigurationProvider(taxRate: 10m, serviceChargeRate: 5m),
            tableService);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            TableId = 1,
            CustomerName = "Member Test",
            IsMember = true,
            MemberId = 1,
            PaymentMethod = PaymentMethod.Transfer,
            Items =
            [
                new OrderItemRequest
                {
                    ProductId = 1,
                    Quantity = 2,
                    Notes = "Tanpa pedas"
                }
            ]
        });

        TestAssert.True(result.Succeeded);
        TestAssert.NotNull(result.Data);

        var transaction = await context.Transactions.SingleAsync();
        TestAssert.Equal("TRX-001", transaction.TransactionNumber);
        TestAssert.Equal(40000m, transaction.Subtotal);
        TestAssert.Equal(4000m, transaction.Tax);
        TestAssert.Equal(2000m, transaction.ServiceCharge);
        TestAssert.Equal(4000m, transaction.Discount);
        TestAssert.Equal(42000m, transaction.Total);
        TestAssert.Equal(PaymentStatus.Pending, transaction.PaymentStatus);
        TestAssert.NotNull(transaction.TableSessionId);

        var detail = await context.TransactionDetails.SingleAsync();
        TestAssert.Equal(2, detail.Quantity);
        TestAssert.Equal("Tanpa pedas", detail.Notes);

        var notification = await context.Notifications.SingleAsync();
        TestAssert.Equal(NotificationType.NewOrder, notification.Type);

        var table = await context.Tables.SingleAsync();
        TestAssert.Equal(TableStatus.Occupied, table.Status);

        var session = await context.TableSessions.SingleAsync();
        TestAssert.Equal(CustomerType.Member, session.CustomerType);
        TestAssert.Equal(1, session.MemberId);
        TestAssert.Equal(now, session.StartTime);
    }

    public static async Task ConfirmPaymentAsync_MarksTransactionAsPaid()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 9, 13, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Transactions.Add(new Transaction
            {
                TransactionNumber = "TRX-002",
                CustomerName = "Guest",
                PaymentMethod = PaymentMethod.Transfer,
                PaymentStatus = PaymentStatus.Pending,
                OrderStatus = OrderStatus.New
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var tableService = new TableService(context, new FixedDateTimeProvider(now));
        var service = new CashierService(
            context,
            new FixedDateTimeProvider(now),
            new StubTransactionNumberGenerator("IGNORED"),
            new StubChargeConfigurationProvider(),
            tableService);

        var result = await service.ConfirmPaymentAsync(1);

        TestAssert.True(result.Succeeded);
        var transaction = await context.Transactions.SingleAsync();
        TestAssert.Equal(PaymentStatus.Paid, transaction.PaymentStatus);
        TestAssert.Equal(now, transaction.PaidAt);
    }

    public static async Task UpdateStatusAsync_PropagatesStatusToOrderDetails()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 9, 14, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Makanan" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Mie Ayam", CategoryId = 1, Price = 25000, IsAvailable = true });
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-003",
                CustomerName = "Guest",
                PaymentMethod = PaymentMethod.Tunai,
                PaymentStatus = PaymentStatus.Paid,
                OrderStatus = OrderStatus.Processing
            });
            arrangeContext.TransactionDetails.Add(new TransactionDetail
            {
                TransactionId = 1,
                ProductId = 1,
                Quantity = 1,
                UnitPrice = 25000,
                Status = DetailStatus.Preparing
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new KitchenService(
            context,
            new FixedDateTimeProvider(now),
            new TableService(context, new FixedDateTimeProvider(now)));

        var result = await service.UpdateStatusAsync(1, OrderStatus.Ready);

        TestAssert.True(result.Succeeded);
        var transaction = await context.Transactions.Include(t => t.TransactionDetails).SingleAsync();
        TestAssert.Equal(OrderStatus.Ready, transaction.OrderStatus);
        TestAssert.All(transaction.TransactionDetails, detail => TestAssert.Equal(DetailStatus.Ready, detail.Status));
        TestAssert.Equal(1, context.Notifications.Count(n => n.Type == NotificationType.OrderReady));
    }

    public static async Task AssetLogService_CreateAsync_DeductsStockAndCreatesReportedLog()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 9, 15, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Users.Add(new User
            {
                Id = 1,
                Username = "supervisor",
                Email = "supervisor@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Supervisor,
                IsActive = true
            });
            arrangeContext.Assets.Add(new Asset
            {
                Id = 1,
                Name = "Piring",
                AssetType = AssetType.Lainnya,
                Quantity = 10,
                Unit = "pcs",
                Condition = AssetCondition.Baik
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AssetLogService(context, new FixedDateTimeProvider(now));

        var result = await service.CreateAsync(new AssetLogViewModel
        {
            AssetId = 1,
            DamageType = DamageType.Pecah,
            Quantity = 3,
            Description = "Tiga piring pecah"
        }, reporterUserId: 1);

        TestAssert.True(result.Succeeded);

        var asset = await context.Assets.SingleAsync();
        TestAssert.Equal(7, asset.Quantity);

        var log = await context.AssetLogs.SingleAsync();
        TestAssert.Equal(LogStatus.Reported, log.Status);
        TestAssert.Equal(3, log.Quantity);
        TestAssert.Equal(now, log.ReportedAt);
    }

    public static async Task AssetLogService_ApproveAsync_SetsApprovalFields()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 9, 16, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Users.AddRange(
                new User
                {
                    Id = 1,
                    Username = "reporter",
                    Email = "reporter@test.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                    Role = UserRole.Supervisor,
                    IsActive = true
                },
                new User
                {
                    Id = 2,
                    Username = "owner",
                    Email = "owner@test.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                    Role = UserRole.Owner,
                    IsActive = true
                });
            arrangeContext.Assets.Add(new Asset
            {
                Id = 1,
                Name = "Gelas",
                AssetType = AssetType.PeralatanMinum,
                Quantity = 5,
                Unit = "pcs",
                Condition = AssetCondition.Baik
            });
            arrangeContext.AssetLogs.Add(new AssetLog
            {
                AssetId = 1,
                DamageType = DamageType.Rusak,
                Quantity = 1,
                ReportedBy = 1,
                Status = LogStatus.Reported
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AssetLogService(context, new FixedDateTimeProvider(now));

        var result = await service.ApproveAsync(1, approverUserId: 2);

        TestAssert.True(result.Succeeded);

        var log = await context.AssetLogs.SingleAsync();
        TestAssert.Equal(LogStatus.Approved, log.Status);
        TestAssert.Equal(2, log.ApprovedBy);
        TestAssert.Equal(now, log.ApprovedAt);
    }
}
