using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class TransactionDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TransactionId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [StringLength(200)]
        public string Notes { get; set; } = string.Empty;

        public DetailStatus Status { get; set; } = DetailStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        [ForeignKey("TransactionId")]
        public virtual Transaction Transaction { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        [NotMapped]
        public decimal Subtotal => Quantity * UnitPrice;
    }

    public enum DetailStatus { Pending, Preparing, Ready, Served }
}
