using System.ComponentModel.DataAnnotations;

namespace Restoran.Models
{
    public class ServiceChargeSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal Percentage { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
