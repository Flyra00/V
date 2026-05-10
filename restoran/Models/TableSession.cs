using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class TableSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TableId { get; set; }

        public CustomerType CustomerType { get; set; } = CustomerType.Guest;

        public int? MemberId { get; set; }

        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        public DateTime StartTime { get; set; } = DateTime.Now;

        public DateTime? EndTime { get; set; }

        public TableSessionStatus Status { get; set; } = TableSessionStatus.Active;

        [ForeignKey(nameof(TableId))]
        public virtual Table Table { get; set; } = null!;

        [ForeignKey(nameof(MemberId))]
        public virtual Member? Member { get; set; }

        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }

    public enum TableSessionStatus
    {
        Active,
        Closed,
        Cancelled
    }
}
