using Restoran.Models;
using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class PaymentMethodSelectionViewModel
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public PaymentMethod LegacyMethod { get; init; }
        public bool IsCustomerFacing { get; init; }
        public bool IsCashierFacing { get; init; }
    }

    public class PaymentMethodFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kode metode pembayaran wajib diisi")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama tampilan wajib diisi")]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipe metode wajib dipilih")]
        public PaymentMethod LegacyMethod { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsCustomerFacing { get; set; } = true;

        public bool IsCashierFacing { get; set; } = true;

        [Range(0, 999)]
        public int SortOrder { get; set; }
    }
}
