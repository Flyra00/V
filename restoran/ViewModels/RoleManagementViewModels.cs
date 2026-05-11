using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class RoleFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama role wajib diisi")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kode role wajib diisi")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        public bool IsSystemRole { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, 999)]
        public int SortOrder { get; set; }
    }
}
