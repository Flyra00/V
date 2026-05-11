using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string TransactionNumber { get; set; } = string.Empty;

        public int? TableId { get; set; }

        public int? TableSessionId { get; set; }

        public int? UserId { get; set; }

        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        public CustomerType CustomerType { get; set; } = CustomerType.Guest;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ServiceCharge { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        public int? PromoId { get; set; }

        [StringLength(100)]
        public string AppliedPromoName { get; set; } = string.Empty;

        public OrderStatus OrderStatus { get; set; } = OrderStatus.New;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("TableId")]
        public virtual Table? Table { get; set; }

        [ForeignKey("TableSessionId")]
        public virtual TableSession? TableSession { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("PromoId")]
        public virtual Promo? Promo { get; set; }

        public virtual Payment? Payment { get; set; }

        public virtual ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();
    }

    public enum CustomerType { Guest, Member }
    public enum PaymentMethod { Tunai, QRIS, Transfer, BayarDiKasir }
    public enum PaymentStatus { Pending, Paid, Failed, Cancelled }
    public enum OrderStatus { New, Processing, Ready, Served, Completed, Cancelled }
}
