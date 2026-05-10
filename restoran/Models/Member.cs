using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restoran.Models
{
    public class Member
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        public MemberType MemberType { get; set; } = MemberType.Regular;

        public int Points { get; set; } = 0;

        public DateTime JoinDate { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [NotMapped]
        public decimal DiscountPercentage => MemberType switch
        {
            MemberType.Silver => 5m,
            MemberType.Gold => 10m,
            MemberType.Platinum => 15m,
            _ => 0m
        };
    }

    public enum MemberType
    {
        Regular,
        Silver,
        Gold,
        Platinum
    }
}
