using Microsoft.EntityFrameworkCore;
using Restoran.Features.Kasir.Services;
using Restoran.Features.Kitchen.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;

namespace Restoran.Tests;

public static class TableSessionTests
{
    public static async Task TableSession_Closes_WhenOrderServedAndPaymentPaid()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var paidAt = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var servedAt = paidAt.AddMinutes(25);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Tables.Add(new Table
            {
                Id = 1,
                TableNumber = "1",
                Capacity = 4,
                Status = TableStatus.Occupied
            });
            arrangeContext.Categories.Add(new Category { Id = 1, Name = "Makanan" });
            arrangeContext.Products.Add(new Product { Id = 1, Name = "Nasi Goreng", CategoryId = 1, Price = 25000, IsAvailable = true });
            arrangeContext.TableSessions.Add(new TableSession
            {
                Id = 1,
                TableId = 1,
                CustomerType = CustomerType.Guest,
                CustomerName = "Budi",
                StartTime = paidAt.AddMinutes(-30),
                Status = TableSessionStatus.Active
            });
            arrangeContext.Transactions.Add(new Transaction
            {
                Id = 1,
                TransactionNumber = "TRX-SESSION-001",
                TableId = 1,
                TableSessionId = 1,
                CustomerName = "Budi",
                CustomerType = CustomerType.Guest,
                OrderStatus = OrderStatus.Ready,
                Subtotal = 25000,
                Tax = 2500,
                ServiceCharge = 1250,
                Total = 28750,
                CreatedAt = paidAt.AddMinutes(-20)
            });
            arrangeContext.TransactionDetails.Add(new TransactionDetail
            {
                TransactionId = 1,
                ProductId = 1,
                Quantity = 1,
                UnitPrice = 25000,
                Status = DetailStatus.Ready
            });
            await TestPaymentData.SeedDefaultPaymentMethodsAsync(arrangeContext);
            await arrangeContext.SaveChangesAsync();
            await TestPaymentData.SeedPaymentAsync(arrangeContext, 1, PaymentMethod.Tunai, PaymentStatus.Pending, 28750m);
        }

        await using (var paymentContext = database.CreateContext())
        {
            var paidService = new CashierService(
                paymentContext,
                new FixedDateTimeProvider(paidAt),
                new StubTransactionNumberGenerator("IGNORED"),
                new StubChargeConfigurationProvider(),
                new TableService(paymentContext, new FixedDateTimeProvider(paidAt)),
                TestPaymentData.CreatePaymentService(paymentContext),
                new StubMidtransService());

            var paymentResult = await paidService.ConfirmPaymentAsync(1, 28750m);
            TestAssert.True(paymentResult.Succeeded);
        }

        await using var serveContext = database.CreateContext();
        var kitchenService = new KitchenService(
            serveContext,
            new FixedDateTimeProvider(servedAt),
            new TableService(serveContext, new FixedDateTimeProvider(servedAt)));

        var serveResult = await kitchenService.UpdateStatusAsync(1, OrderStatus.Served);
        TestAssert.True(serveResult.Succeeded);

        var session = await serveContext.TableSessions.SingleAsync();
        var table = await serveContext.Tables.SingleAsync();
        TestAssert.Equal(TableSessionStatus.Closed, session.Status);
        TestAssert.Equal(servedAt, session.EndTime);
        TestAssert.Equal(TableStatus.Available, table.Status);
    }

    public static async Task TableService_CreateAsync_RejectsDuplicateTableNumber()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        context.Tables.Add(new Table
        {
            TableNumber = "A1",
            Capacity = 4,
            Status = TableStatus.Available
        });
        await context.SaveChangesAsync();

        var service = new TableService(context, new FixedDateTimeProvider(DateTime.UtcNow));
        var result = await service.CreateAsync(new Restoran.ViewModels.TableFormViewModel
        {
            TableNumber = "a1",
            Capacity = 4,
            Status = TableStatus.Available
        });

        TestAssert.False(result.Succeeded);
        TestAssert.Equal("Nomor meja sudah digunakan", result.Message);
    }

    public static async Task TableService_GetCustomerTableOptionsAsync_MarksDisabledAndOccupiedTablesAsUnavailable()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Tables.AddRange(
                new Table { Id = 1, TableNumber = "1", Capacity = 4, Status = TableStatus.Available },
                new Table { Id = 2, TableNumber = "2", Capacity = 4, Status = TableStatus.Reserved },
                new Table { Id = 3, TableNumber = "3", Capacity = 4, Status = TableStatus.Disabled },
                new Table { Id = 4, TableNumber = "4", Capacity = 4, Status = TableStatus.Available });
            arrangeContext.TableSessions.Add(new TableSession
            {
                Id = 1,
                TableId = 4,
                CustomerType = CustomerType.Guest,
                CustomerName = "Rina",
                StartTime = now.AddMinutes(-10),
                Status = TableSessionStatus.Active
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new TableService(context, new FixedDateTimeProvider(now));

        var options = await service.GetCustomerTableOptionsAsync();

        var available = options.Single(option => option.Id == 1);
        var reserved = options.Single(option => option.Id == 2);
        var disabled = options.Single(option => option.Id == 3);
        var occupied = options.Single(option => option.Id == 4);

        TestAssert.True(available.CanStartOrder);
        TestAssert.Equal("Tersedia", available.StatusLabel);

        TestAssert.False(reserved.CanStartOrder);
        TestAssert.Equal("Reservasi", reserved.StatusLabel);

        TestAssert.False(disabled.CanStartOrder);
        TestAssert.Equal("Nonaktif", disabled.StatusLabel);

        TestAssert.False(occupied.CanStartOrder);
        TestAssert.Equal("Sedang Dipakai", occupied.StatusLabel);
        TestAssert.Equal(TableStatus.Occupied, occupied.Status);
    }

    public static async Task TableService_DeactivateAndReactivateAsync_TogglesDisabledStatus()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var arrangeContext = database.CreateContext();
        arrangeContext.Tables.Add(new Table
        {
            Id = 1,
            TableNumber = "A1",
            Capacity = 4,
            Status = TableStatus.Available
        });
        await arrangeContext.SaveChangesAsync();

        await using var context = database.CreateContext();
        var service = new TableService(context, new FixedDateTimeProvider(DateTime.UtcNow));

        var deactivateResult = await service.DeactivateAsync(1);
        TestAssert.True(deactivateResult.Succeeded);
        TestAssert.Equal("Meja berhasil dinonaktifkan", deactivateResult.Message);
        TestAssert.Equal(TableStatus.Disabled, (await context.Tables.SingleAsync()).Status);

        var reactivateResult = await service.ReactivateAsync(1);
        TestAssert.True(reactivateResult.Succeeded);
        TestAssert.Equal("Meja berhasil diaktifkan kembali", reactivateResult.Message);
        TestAssert.Equal(TableStatus.Available, (await context.Tables.SingleAsync()).Status);
    }

    public static async Task TableService_DeactivateAsync_CancelsActiveSession_AndDisabledSessionCreation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Tables.AddRange(
                new Table { Id = 1, TableNumber = "1", Capacity = 4, Status = TableStatus.Occupied },
                new Table { Id = 2, TableNumber = "2", Capacity = 4, Status = TableStatus.Disabled });
            arrangeContext.TableSessions.Add(new TableSession
            {
                Id = 1,
                TableId = 1,
                CustomerType = CustomerType.Guest,
                CustomerName = "Doni",
                StartTime = now.AddMinutes(-20),
                Status = TableSessionStatus.Active
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new TableService(context, new FixedDateTimeProvider(now));

        var deactivateResult = await service.DeactivateAsync(1);
        TestAssert.True(deactivateResult.Succeeded);
        TestAssert.Equal("Meja berhasil dinonaktifkan dan sesi aktif dibatalkan", deactivateResult.Message);

        var session = await context.TableSessions.SingleAsync(entity => entity.Id == 1);
        TestAssert.Equal(TableSessionStatus.Cancelled, session.Status);
        TestAssert.Equal(now, session.EndTime);
        TestAssert.Equal(TableStatus.Disabled, (await context.Tables.SingleAsync(entity => entity.Id == 1)).Status);

        var threw = false;
        try
        {
            await service.EnsureActiveSessionAsync(2, CustomerType.Guest, null, "Dina");
        }
        catch (InvalidOperationException ex)
        {
            threw = true;
            TestAssert.Equal("Meja ini sedang tidak tersedia", ex.Message);
        }

        TestAssert.True(threw, "Meja nonaktif seharusnya tidak dapat membuat sesi aktif.");
    }
}
