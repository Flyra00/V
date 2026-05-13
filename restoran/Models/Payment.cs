using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        public int TransactionId { get; set; }

        public int PaymentMethodOptionId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountReceived { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChangeAmount { get; set; }

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public DateTime? PaymentDate { get; set; }

        [StringLength(200)]
        public string ProofUrl { get; set; } = string.Empty;

        [StringLength(100)]
        public string MidtransOrderId { get; set; } = string.Empty;

        [StringLength(200)]
        public string MidtransTransactionId { get; set; } = string.Empty;

        [StringLength(100)]
        public string MidtransPaymentType { get; set; } = string.Empty;

        [StringLength(50)]
        public string MidtransTransactionStatus { get; set; } = string.Empty;

        [StringLength(50)]
        public string MidtransFraudStatus { get; set; } = string.Empty;

        [StringLength(200)]
        public string SnapToken { get; set; } = string.Empty;

        [StringLength(500)]
        public string SnapRedirectUrl { get; set; } = string.Empty;

        public string ProviderResponseJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(TransactionId))]
        public virtual Transaction Transaction { get; set; } = null!;

        [ForeignKey(nameof(PaymentMethodOptionId))]
        public virtual PaymentMethodOption PaymentMethodOption { get; set; } = null!;
    }
}
