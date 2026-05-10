using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Asset
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama aset wajib diisi")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public AssetType AssetType { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = string.Empty;

        public AssetCondition Condition { get; set; } = AssetCondition.Baik;

        public DateTime? PurchaseDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PurchasePrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<AssetLog> AssetLogs { get; set; } = new List<AssetLog>();
    }

    public enum AssetType
    {
        PeralatanDapur,
        PeralatanMinum,
        Meja,
        Kursi,
        PeralatanElektronik,
        Lainnya
    }

    public enum AssetCondition
    {
        Baik,
        RusakRingan,
        RusakBerat,
        Hilang
    }
}
