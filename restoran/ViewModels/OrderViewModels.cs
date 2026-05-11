using Restoran.Models;
using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class OrderMenuViewModel
    {
        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string TaxName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public string ServiceChargeName { get; set; } = string.Empty;
        public decimal ServiceChargeRate { get; set; }
        public IReadOnlyList<PromoSummaryViewModel> ActivePromos { get; set; } = Array.Empty<PromoSummaryViewModel>();
        public IReadOnlyList<PaymentMethodSelectionViewModel> PaymentMethods { get; set; } = Array.Empty<PaymentMethodSelectionViewModel>();
        public Dictionary<string, List<Product>> ProductsByCategory { get; set; } = new();
    }

    public class CreateOrderRequest
    {
        public int TableId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public bool IsMember { get; set; }
        public int? MemberId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class KitchenOrderViewModel
    {
        public int TransactionId { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public List<KitchenItemViewModel> Items { get; set; } = new();
    }

    public class KitchenItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DetailStatus Status { get; set; }
    }

    public class PrintReceiptViewModel
    {
        public string TransactionNumber { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<ReceiptItemViewModel> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
    }

    public class ReceiptItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
