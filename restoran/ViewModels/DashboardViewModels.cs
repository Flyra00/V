namespace Restoran.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TodayRevenue { get; set; }
        public int TodayTransactionCount { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int MonthlyTransactionCount { get; set; }
        public List<TopProductViewModel> TopProductsToday { get; set; } = new();
        public int AvailableProductsCount { get; set; }
        public int UnavailableProductsCount { get; set; }
        public List<LowStockViewModel> LowStockIngredients { get; set; } = new();
        public List<AssetDamageViewModel> RecentAssetDamages { get; set; } = new();
        public List<ActiveTransactionViewModel> ActiveTransactions { get; set; } = new();
        public List<ChartDataViewModel> RevenueChartData { get; set; } = new();
    }

    public class TopProductViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class LowStockViewModel
    {
        public string Name { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal MinStock { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class AssetDamageViewModel
    {
        public string AssetName { get; set; } = string.Empty;
        public string DamageType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime ReportedAt { get; set; }
    }

    public class ActiveTransactionViewModel
    {
        public string TransactionNumber { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChartDataViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class RevenueReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalServiceCharge { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageTransactionValue { get; set; }
        public string TaxName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public string ServiceChargeName { get; set; } = string.Empty;
        public decimal ServiceChargeRate { get; set; }
        public List<DailyRevenueViewModel> DailyRevenues { get; set; } = new();
        public List<PaymentMethodSummaryViewModel> PaymentMethods { get; set; } = new();
    }

    public class DailyRevenueViewModel
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int TransactionCount { get; set; }
    }

    public class PaymentMethodSummaryViewModel
    {
        public string Method { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class StockReportViewModel
    {
        public List<IngredientStockViewModel> Ingredients { get; set; } = new();
        public List<AssetStockViewModel> Assets { get; set; } = new();
        public List<ProductAvailabilityViewModel> ProductsAvailability { get; set; } = new();
    }

    public class IngredientStockViewModel
    {
        public string Name { get; set; } = string.Empty;
        public decimal StockQuantity { get; set; }
        public decimal MinStock { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsLowStock { get; set; }
    }

    public class AssetStockViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
    }

    public class ProductAvailabilityViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public decimal Price { get; set; }
    }
}
