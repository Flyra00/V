namespace Restoran.Features.Orders.Dtos
{
    public class CreateOrderResponse
    {
        public int TransactionId { get; init; }
        public string TrackingToken { get; init; } = string.Empty;
        public string TransactionNumber { get; init; } = string.Empty;
        public string AppliedPromoName { get; init; } = string.Empty;
        public decimal DiscountAmount { get; init; }
        public bool IsMidtransPayment { get; init; }
        public string PaymentRedirectUrl { get; init; } = string.Empty;
        public string SnapToken { get; init; } = string.Empty;
    }
}
