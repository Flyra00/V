using Restoran.Models;

namespace Restoran.Features.Kasir.Dtos
{
    public class CreatePosOrderRequest
    {
        public int? TableId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public int? PromoId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public List<PosOrderItemRequest> Items { get; set; } = new();
    }

    public class PosOrderItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
