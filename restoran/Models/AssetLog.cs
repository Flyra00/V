using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class AssetLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AssetId { get; set; }

        [Required]
        public DamageType DamageType { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        public int ReportedBy { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime ReportedAt { get; set; } = DateTime.Now;

        public LogStatus Status { get; set; } = LogStatus.Reported;

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedBy { get; set; }

        [ForeignKey("AssetId")]
        public virtual Asset Asset { get; set; } = null!;

        [ForeignKey("ReportedBy")]
        public virtual User Reporter { get; set; } = null!;

        [ForeignKey("ApprovedBy")]
        public virtual User? Approver { get; set; }
    }

    public enum DamageType
    {
        Pecah,
        Rusak,
        Hilang,
        Aus
    }

    public enum LogStatus
    {
        Reported,
        Approved,
        Rejected
    }
}
