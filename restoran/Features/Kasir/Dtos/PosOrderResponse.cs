namespace Restoran.Features.Kasir.Dtos
{
    public class PosOrderResponse
    {
        public int TransactionId { get; init; }
        public decimal Total { get; init; }
    }
}
