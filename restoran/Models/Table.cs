using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Table
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string TableNumber { get; set; } = string.Empty;

        [Required]
        [Range(1, 20)]
        public int Capacity { get; set; }

        [StringLength(200)]
        public string QrCodeUrl { get; set; } = string.Empty;

        public TableStatus Status { get; set; } = TableStatus.Available;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public virtual ICollection<TableSession> TableSessions { get; set; } = new List<TableSession>();
    }

    public enum TableStatus
    {
        Available,
        Occupied,
        Reserved,
        Disabled
    }
}
