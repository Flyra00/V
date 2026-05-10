using System.ComponentModel.DataAnnotations;

namespace Restoran.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public int? TransactionId { get; set; }

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [StringLength(50)]
        public string Recipient { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Transaction? Transaction { get; set; }
    }

    public enum NotificationType
    {
        NewOrder,
        OrderReady,
        OrderServed,
        PaymentReceived,
        LowStock,
        AssetDamage
    }
}
