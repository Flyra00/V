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
                PaymentMethod = PaymentMethod.Transfer,
                PaymentStatus = PaymentStatus.Pending,
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
            await arrangeContext.SaveChangesAsync();
        }

        await using (var paymentContext = database.CreateContext())
        {
            var paidService = new CashierService(
                paymentContext,
                new FixedDateTimeProvider(paidAt),
                new StubTransactionNumberGenerator("IGNORED"),
                new StubChargeConfigurationProvider(),
                new TableService(paymentContext, new FixedDateTimeProvider(paidAt)));

            var paymentResult = await paidService.ConfirmPaymentAsync(1);
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
}
