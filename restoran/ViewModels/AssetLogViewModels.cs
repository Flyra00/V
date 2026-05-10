using Restoran.Models;
using Restoran.Features.Supervisor.Dtos;
using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class AssetLogViewModel
    {
        [Required(ErrorMessage = "Pilih aset yang rusak")]
        public int AssetId { get; set; }

        [Required(ErrorMessage = "Pilih jenis kerusakan")]
        public DamageType DamageType { get; set; }

        [Required(ErrorMessage = "Masukkan jumlah")]
        [Range(1, int.MaxValue, ErrorMessage = "Jumlah minimal 1")]
        public int Quantity { get; set; }

        [StringLength(500, ErrorMessage = "Deskripsi maksimal 500 karakter")]
        public string Description { get; set; } = string.Empty;

        public IReadOnlyList<AssetLookupItem> AvailableAssets { get; set; } = Array.Empty<AssetLookupItem>();
    }

    public class AssetLogListViewModel
    {
        public int Id { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public DamageType DamageType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string ReporterName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; }
        public LogStatus Status { get; set; }
    }

    public class AssetDamageReportViewModel
    {
        public int TotalDamageReports { get; set; }
        public int TotalApproved { get; set; }
        public int TotalPending { get; set; }
        public List<AssetDamageItemViewModel> RecentDamages { get; set; } = new();
        public List<DamageTypeSummaryViewModel> DamageByType { get; set; } = new();
    }

    public class AssetDamageItemViewModel
    {
        public string AssetName { get; set; } = string.Empty;
        public DamageType DamageType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; }
    }

    public class DamageTypeSummaryViewModel
    {
        public DamageType DamageType { get; set; }
        public int Count { get; set; }
        public int TotalQuantity { get; set; }
    }
}
