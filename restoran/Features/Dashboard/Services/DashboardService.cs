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

            return new DashboardViewModel
            {
                TodayRevenue = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt.HasValue && t.PaidAt.Value.Date == today)
                    .SumAsync(t => t.Total, cancellationToken),
                TodayTransactionCount = await _context.Transactions
                    .CountAsync(t => t.CreatedAt.Date == today, cancellationToken),
                MonthlyRevenue = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt.HasValue && t.PaidAt.Value >= firstDayOfMonth)
                    .SumAsync(t => t.Total, cancellationToken),
                MonthlyTransactionCount = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= firstDayOfMonth, cancellationToken),
                TopProductsToday = await _context.TransactionDetails
                    .Where(td => td.Transaction.CreatedAt.Date == today)
                    .GroupBy(td => td.Product.Name)
                    .Select(g => new TopProductViewModel
                    {
                        ProductName = g.Key,
                        QuantitySold = g.Sum(td => td.Quantity),
                        Revenue = g.Sum(td => td.Quantity * td.UnitPrice)
                    })
                    .OrderByDescending(x => x.QuantitySold)
                    .Take(5)
                    .ToListAsync(cancellationToken),
                AvailableProductsCount = await _context.Products.CountAsync(p => p.IsAvailable, cancellationToken),
                UnavailableProductsCount = await _context.Products.CountAsync(p => !p.IsAvailable, cancellationToken),
                LowStockIngredients = await _context.Assets
                    .Where(asset => asset.Condition != AssetCondition.Baik || asset.Quantity <= 5)
                    .Select(asset => new LowStockViewModel
                    {
                        Name = asset.Name,
                        CurrentStock = asset.Quantity,
                        MinStock = 5,
                        Unit = asset.Unit
                    })
                    .ToListAsync(cancellationToken),
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
                RevenueChartData = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt.HasValue && t.PaidAt.Value >= lastWeek)
                    .GroupBy(t => t.PaidAt!.Value.Date)
                    .Select(g => new ChartDataViewModel
                    {
                        Label = g.Key.ToString("dd/MM"),
                        Value = g.Sum(t => t.Total)
                    })
                    .OrderBy(x => x.Label)
                    .ToListAsync(cancellationToken)
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

            return new RevenueReportViewModel
            {
                FromDate = effectiveFrom,
                ToDate = effectiveTo,
                TotalRevenue = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt >= effectiveFrom && t.PaidAt <= toInclusive)
                    .SumAsync(t => t.Total, cancellationToken),
                TotalTransactions = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= effectiveFrom && t.CreatedAt <= toInclusive, cancellationToken),
                TotalTax = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt >= effectiveFrom && t.PaidAt <= toInclusive)
                    .SumAsync(t => t.Tax, cancellationToken),
                TotalServiceCharge = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt >= effectiveFrom && t.PaidAt <= toInclusive)
                    .SumAsync(t => t.ServiceCharge, cancellationToken),
                AverageTransactionValue = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt >= effectiveFrom && t.PaidAt <= toInclusive)
                    .AverageAsync(t => (decimal?)t.Total, cancellationToken) ?? 0,
                TaxName = chargeConfiguration.TaxName,
                TaxRate = chargeConfiguration.TaxRate,
                ServiceChargeName = chargeConfiguration.ServiceChargeName,
                ServiceChargeRate = chargeConfiguration.ServiceChargeRate,
                DailyRevenues = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt >= effectiveFrom && t.PaidAt <= toInclusive)
                    .GroupBy(t => t.PaidAt!.Value.Date)
                    .Select(g => new DailyRevenueViewModel
                    {
                        Date = g.Key,
                        Revenue = g.Sum(t => t.Total),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync(cancellationToken),
                PaymentMethods = await _context.Transactions
                    .Where(t => t.PaymentStatus == PaymentStatus.Paid && t.PaidAt >= effectiveFrom && t.PaidAt <= toInclusive)
                    .GroupBy(t => t.PaymentMethod)
                    .Select(group => new PaymentMethodSummaryViewModel
                    {
                        Method = group.Key.ToString(),
                        TransactionCount = group.Count(),
                        TotalAmount = group.Sum(transaction => transaction.Total)
                    })
                    .OrderByDescending(item => item.TotalAmount)
                    .ToListAsync(cancellationToken)
            };
        }

        public async Task<StockReportViewModel> GetStockReportAsync(CancellationToken cancellationToken = default)
        {
            return new StockReportViewModel
            {
                Ingredients = await _context.Ingredients
                    .Select(i => new IngredientStockViewModel
                    {
                        Name = i.Name,
                        StockQuantity = i.StockQuantity,
                        MinStock = i.MinStock,
                        Unit = i.Unit,
                        IsLowStock = i.StockQuantity <= i.MinStock
                    })
                    .OrderBy(i => i.Name)
                    .ToListAsync(cancellationToken),
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
