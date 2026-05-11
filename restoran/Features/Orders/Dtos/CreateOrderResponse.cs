namespace Restoran.Features.Orders.Dtos
{
    public class CreateOrderResponse
    {
        public int TransactionId { get; init; }
        public string TransactionNumber { get; init; } = string.Empty;
        public string AppliedPromoName { get; init; } = string.Empty;
        public decimal DiscountAmount { get; init; }
    }
}
