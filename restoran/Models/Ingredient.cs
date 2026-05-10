using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Ingredient
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama bahan wajib diisi")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Stok tidak boleh negatif")]
        public decimal StockQuantity { get; set; }

        [Required]
        public decimal MinStock { get; set; } = 10;

        [StringLength(100)]
        public string Supplier { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastUpdated { get; set; }

        public virtual ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();

        [NotMapped]
        public bool IsLowStock => StockQuantity <= MinStock;
    }
}
