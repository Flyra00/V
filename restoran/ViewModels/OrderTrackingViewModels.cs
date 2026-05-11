using Restoran.Models;

namespace Restoran.ViewModels
{
    public class OrderTrackingViewModel
    {
        public bool IsEmptyState { get; init; }
        public int TransactionId { get; init; }
        public string TransactionNumber { get; init; } = string.Empty;
        public int? TableId { get; init; }
        public string TableNumber { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public OrderStatus OrderStatus { get; init; }
        public PaymentMethod PaymentMethod { get; init; }
        public string PaymentMethodDisplayName { get; init; } = string.Empty;
        public PaymentStatus PaymentStatus { get; init; }
        public DateTime? PaidAt { get; init; }
        public decimal Subtotal { get; init; }
        public decimal Discount { get; init; }
        public decimal Tax { get; init; }
        public decimal ServiceCharge { get; init; }
        public decimal Total { get; init; }
        public string AppliedPromoName { get; init; } = string.Empty;
        public string PaymentProofUrl { get; init; } = string.Empty;
        public int RefreshIntervalSeconds { get; init; } = 15;
        public bool RequiresOnlinePaymentProof => PaymentMethod is PaymentMethod.QRIS or PaymentMethod.Transfer && PaymentStatus != PaymentStatus.Paid;
        public bool IsTrackingFinal => OrderStatus is OrderStatus.Completed or OrderStatus.Cancelled ||
                                       (OrderStatus == OrderStatus.Served && PaymentStatus is PaymentStatus.Paid or PaymentStatus.Cancelled);
        public IReadOnlyList<OrderTrackingItemViewModel> Items { get; init; } = Array.Empty<OrderTrackingItemViewModel>();
    }

    public class OrderTrackingItemViewModel
    {
        public int DetailId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public string Notes { get; init; } = string.Empty;
        public DetailStatus Status { get; init; }
    }

    public class OrderTrackingStatusResponse
    {
        public int TransactionId { get; init; }
        public OrderStatus OrderStatus { get; init; }
        public PaymentStatus PaymentStatus { get; init; }
        public DateTime? PaidAt { get; init; }
        public bool IsTrackingFinal { get; init; }
        public DateTime RefreshedAt { get; init; }
        public IReadOnlyList<OrderTrackingItemViewModel> Items { get; init; } = Array.Empty<OrderTrackingItemViewModel>();
    }
}
