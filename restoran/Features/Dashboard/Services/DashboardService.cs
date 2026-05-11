using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Dashboard.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IChargeConfigurationProvider _chargeConfigurationProvider;

        public DashboardService(
            ApplicationDbContext context,
            IDateTimeProvider dateTimeProvider,
            IChargeConfigurationProvider chargeConfigurationProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _chargeConfigurationProvider = chargeConfigurationProvider;
        }

        public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
        {
            var today = _dateTimeProvider.Now.Date;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var thirtyDaysAgo = _dateTimeProvider.Now.AddDays(-30);
            var lastWeek = _dateTimeProvider.Now.AddDays(-7);
            var paidTodayTotals = await _context.Payments
                .Where(payment => payment.PaymentStatus == PaymentStatus.Paid &&
                                  payment.PaymentDate.HasValue &&
                                  payment.PaymentDate.Value.Date == today)
                .Select(payment => payment.Amount)
                .ToListAsync(cancellationToken);
            var paidMonthlyTotals = await _context.Payments
                .Where(payment => payment.PaymentStatus == PaymentStatus.Paid &&
                                  payment.PaymentDate.HasValue &&
                                  payment.PaymentDate.Value >= firstDayOfMonth)
                .Select(payment => payment.Amount)
                .ToListAsync(cancellationToken);
            var topProductRows = await _context.TransactionDetails
                .Where(td => td.Transaction.CreatedAt.Date == today)
                .Select(td => new
                {
                    ProductName = td.Product.Name,
                    td.Quantity,
                    td.UnitPrice
                })
                .ToListAsync(cancellationToken);
            var paidTransactionsForChart = await _context.Payments
                .Where(payment => payment.PaymentStatus == PaymentStatus.Paid &&
                                  payment.PaymentDate.HasValue &&
                                  payment.PaymentDate.Value >= lastWeek)
                .Select(t => new
                {
                    Date = t.PaymentDate!.Value.Date,
                    t.Amount
                })
                .ToListAsync(cancellationToken);

            return new DashboardViewModel
            {
                TodayRevenue = paidTodayTotals.Sum(),
                TodayTransactionCount = await _context.Transactions
                    .CountAsync(t => t.CreatedAt.Date == today, cancellationToken),
                MonthlyRevenue = paidMonthlyTotals.Sum(),
                MonthlyTransactionCount = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= firstDayOfMonth, cancellationToken),
                TopProductsToday = topProductRows
                    .GroupBy(row => row.ProductName)
                    .Select(group => new TopProductViewModel
                    {
                        ProductName = group.Key,
                        QuantitySold = group.Sum(row => row.Quantity),
                        Revenue = group.Sum(row => row.Quantity * row.UnitPrice)
                    })
                    .OrderByDescending(x => x.QuantitySold)
                    .Take(5)
                    .ToList(),
                AvailableProductsCount = await _context.Products.CountAsync(p => p.IsAvailable, cancellationToken),
                UnavailableProductsCount = await _context.Products.CountAsync(p => !p.IsAvailable, cancellationToken),
                InventoryAlerts = (await _context.Assets
                    .Where(asset => asset.Condition != AssetCondition.Baik || asset.Quantity <= 5)
                    .Select(asset => new InventoryAlertViewModel
                    {
                        Name = asset.Name,
                        CurrentQuantity = asset.Quantity,
                        AlertThreshold = 5,
                        Unit = asset.Unit,
                        Condition = asset.Condition.ToString()
                    })
                    .ToListAsync(cancellationToken))
                    .OrderBy(asset => asset.CurrentQuantity)
                    .ToList(),
                RecentAssetDamages = await _context.AssetLogs
                    .Include(al => al.Asset)
                    .Where(al => al.ReportedAt >= thirtyDaysAgo)
                    .OrderByDescending(al => al.ReportedAt)
                    .Take(5)
                    .Select(al => new AssetDamageViewModel
                    {
                        AssetName = al.Asset.Name,
                        DamageType = al.DamageType.ToString(),
                        Quantity = al.Quantity,
                        ReportedAt = al.ReportedAt
                    })
                    .ToListAsync(cancellationToken),
                ActiveTransactions = await _context.Transactions
                    .Include(t => t.Table)
                    .Where(t => t.OrderStatus != OrderStatus.Completed && t.OrderStatus != OrderStatus.Cancelled)
                    .OrderBy(t => t.CreatedAt)
                    .Select(t => new ActiveTransactionViewModel
                    {
                        TransactionNumber = t.TransactionNumber,
                        TableNumber = t.Table != null ? t.Table.TableNumber : "Takeaway",
                        OrderStatus = t.OrderStatus.ToString(),
                        Total = t.Total,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync(cancellationToken),
                RevenueChartData = paidTransactionsForChart
                    .GroupBy(transaction => transaction.Date)
                    .Select(group => new ChartDataViewModel
                    {
                        Label = group.Key.ToString("dd/MM"),
                        Value = group.Sum(transaction => transaction.Amount)
                    })
                    .OrderBy(x => x.Label)
                    .ToList()
            };
        }

        public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Products.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreateProductAsync(Product product, CancellationToken cancellationToken = default)
        {
            product.CreatedAt = _dateTimeProvider.Now;
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Menu berhasil ditambahkan");
        }

        public async Task<OperationResult> UpdateProductAsync(int id, Product product, CancellationToken cancellationToken = default)
        {
            if (id != product.Id)
            {
                return OperationResult.NotFound("Menu tidak ditemukan");
            }

            var existingProduct = await _context.Products.FindAsync([id], cancellationToken);
            if (existingProduct == null)
            {
                return OperationResult.NotFound("Menu tidak ditemukan");
            }

            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.MemberDiscountPercentage = product.MemberDiscountPercentage;
            existingProduct.IsAvailable = product.IsAvailable;
            existingProduct.ImageUrl = product.ImageUrl;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Menu berhasil diupdate");
        }

        public async Task<OperationResult> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
        {
            var product = await _context.Products.FindAsync([id], cancellationToken);
            if (product == null)
            {
                return OperationResult.NotFound("Menu tidak ditemukan");
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Menu berhasil dihapus");
        }

        public async Task<RevenueReportViewModel> GetRevenueReportAsync(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default)
        {
            var effectiveFrom = fromDate ?? _dateTimeProvider.Now.AddDays(-30);
            var effectiveTo = toDate ?? _dateTimeProvider.Now;
            var toInclusive = effectiveTo.AddDays(1);
            var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);
            var paidTransactions = await _context.Payments
                .AsNoTracking()
                .Include(payment => payment.Transaction)
                .Include(payment => payment.PaymentMethodOption)
                .Where(payment => payment.PaymentStatus == PaymentStatus.Paid &&
                                  payment.PaymentDate >= effectiveFrom &&
                                  payment.PaymentDate <= toInclusive)
                .Select(payment => new
                {
                    PaidDate = payment.PaymentDate!.Value.Date,
                    payment.Transaction.Total,
                    payment.Transaction.Tax,
                    payment.Transaction.ServiceCharge,
                    PaymentMethodName = payment.PaymentMethodOption.DisplayName
                })
                .ToListAsync(cancellationToken);

            return new RevenueReportViewModel
            {
                FromDate = effectiveFrom,
                ToDate = effectiveTo,
                TotalRevenue = paidTransactions.Sum(transaction => transaction.Total),
                TotalTransactions = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= effectiveFrom && t.CreatedAt <= toInclusive, cancellationToken),
                TotalTax = paidTransactions.Sum(transaction => transaction.Tax),
                TotalServiceCharge = paidTransactions.Sum(transaction => transaction.ServiceCharge),
                AverageTransactionValue = paidTransactions.Count > 0
                    ? paidTransactions.Average(transaction => transaction.Total)
                    : 0,
                TaxName = chargeConfiguration.TaxName,
                TaxRate = chargeConfiguration.TaxRate,
                ServiceChargeName = chargeConfiguration.ServiceChargeName,
                ServiceChargeRate = chargeConfiguration.ServiceChargeRate,
                DailyRevenues = paidTransactions
                    .GroupBy(transaction => transaction.PaidDate)
                    .Select(group => new DailyRevenueViewModel
                    {
                        Date = group.Key,
                        Revenue = group.Sum(transaction => transaction.Total),
                        TransactionCount = group.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToList(),
                PaymentMethods = paidTransactions
                    .GroupBy(transaction => transaction.PaymentMethodName)
                    .Select(group => new PaymentMethodSummaryViewModel
                    {
                        Method = group.Key,
                        TransactionCount = group.Count(),
                        TotalAmount = group.Sum(transaction => transaction.Total)
                    })
                    .OrderByDescending(item => item.TotalAmount)
                    .ToList()
            };
        }

        public async Task<StockReportViewModel> GetStockReportAsync(CancellationToken cancellationToken = default)
        {
            return new StockReportViewModel
            {
                Assets = await _context.Assets
                    .Select(a => new AssetStockViewModel
                    {
                        Name = a.Name,
                        AssetType = a.AssetType.ToString(),
                        Quantity = a.Quantity,
                        Unit = a.Unit,
                        Condition = a.Condition.ToString()
                    })
                    .OrderBy(a => a.Name)
                    .ToListAsync(cancellationToken),
                ProductsAvailability = await _context.Products
                    .Include(p => p.Category)
                    .Select(p => new ProductAvailabilityViewModel
                    {
                        Name = p.Name,
                        Category = p.Category.Name,
                        IsAvailable = p.IsAvailable,
                        Price = p.Price
                    })
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToListAsync(cancellationToken)
            };
        }
    }
}
