using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Promo
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama promo wajib diisi")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public PromoType PromoType { get; set; } = PromoType.Percentage;

        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100, ErrorMessage = "Nilai promo harus di antara 0 sampai 100")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum pembelian tidak boleh negatif")]
        public decimal MinimumPurchase { get; set; }

        public DateTime StartsAt { get; set; }

        public DateTime EndsAt { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }

    public enum PromoType
    {
        Percentage
    }
}
