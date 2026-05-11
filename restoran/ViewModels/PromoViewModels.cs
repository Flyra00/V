using Restoran.Models;
using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class PromoFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama promo wajib diisi")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public PromoType PromoType { get; set; } = PromoType.Percentage;

        [Range(0, 100, ErrorMessage = "Diskon promo harus di antara 0 sampai 100")]
        public decimal DiscountValue { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Minimum pembelian tidak boleh negatif")]
        public decimal MinimumPurchase { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime StartsAt { get; set; } = DateTime.Today;

        [DataType(DataType.DateTime)]
        public DateTime EndsAt { get; set; } = DateTime.Today.AddDays(7).AddHours(23).AddMinutes(59);

        public bool IsActive { get; set; } = true;
    }

    public class PromoSummaryViewModel
    {
        public string Name { get; init; } = string.Empty;
        public decimal DiscountPercentage { get; init; }
        public decimal MinimumPurchase { get; init; }
        public DateTime EndsAt { get; init; }
    }
}
