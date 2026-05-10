using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class ChargeSettingsViewModel
    {
        public int? TaxSettingId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Nama Pajak")]
        public string TaxName { get; set; } = "PPN";

        [Range(0, 100)]
        [Display(Name = "Persentase Pajak")]
        public decimal TaxPercentage { get; set; } = 10m;

        [Display(Name = "Pajak Aktif")]
        public bool IsTaxActive { get; set; } = true;

        public int? ServiceChargeSettingId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Nama Service Charge")]
        public string ServiceChargeName { get; set; } = "Service Charge";

        [Range(0, 100)]
        [Display(Name = "Persentase Service Charge")]
        public decimal ServiceChargePercentage { get; set; } = 5m;

        [Display(Name = "Service Charge Aktif")]
        public bool IsServiceChargeActive { get; set; } = true;

        public decimal ExampleSubtotal { get; set; } = 100000m;

        public decimal ExampleTaxAmount => IsTaxActive ? ExampleSubtotal * (TaxPercentage / 100m) : 0m;

        public decimal ExampleServiceChargeAmount => IsServiceChargeActive ? ExampleSubtotal * (ServiceChargePercentage / 100m) : 0m;

        public decimal ExampleTotal => ExampleSubtotal + ExampleTaxAmount + ExampleServiceChargeAmount;
    }
}
