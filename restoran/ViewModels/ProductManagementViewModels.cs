using Microsoft.AspNetCore.Http;
using Restoran.Features.Admin.Dtos;
using Restoran.Features.Supervisor.Dtos;
using Restoran.Models;
using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class ProductManagementViewModel
    {
        public IReadOnlyList<Product> Products { get; init; } = Array.Empty<Product>();
        public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
    }

    public class ProductFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama produk wajib diisi")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Range(0, 100)]
        public decimal MemberDiscountPercentage { get; set; }

        public bool IsAvailable { get; set; } = true;

        public string ExistingImageUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public IFormFile? ImageFile { get; set; }

        public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
    }

    public class AdminUserFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Username wajib diisi")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email wajib diisi")]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role wajib dipilih")]
        public int? RoleId { get; set; }

        public bool IsActive { get; set; } = true;

        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        public string SelectedRoleName { get; set; } = string.Empty;

        public IReadOnlyList<RoleOption> AvailableRoles { get; set; } = Array.Empty<RoleOption>();
    }

    public class PosPageViewModel
    {
        public IReadOnlyList<Product> Products { get; init; } = Array.Empty<Product>();
        public IReadOnlyList<Table> Tables { get; init; } = Array.Empty<Table>();
        public IReadOnlyList<PaymentMethodSelectionViewModel> PaymentMethods { get; init; } = Array.Empty<PaymentMethodSelectionViewModel>();
        public IReadOnlyList<Promo> ActivePromos { get; init; } = Array.Empty<Promo>();
    }
}
