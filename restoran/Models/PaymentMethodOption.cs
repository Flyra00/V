using System.ComponentModel.DataAnnotations;

namespace Restoran.Models
{
    public class PaymentMethodOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        public PaymentMethod LegacyMethod { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsCustomerFacing { get; set; } = true;

        public bool IsCashierFacing { get; set; } = true;

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
