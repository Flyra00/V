using Restoran.Models;
using System.ComponentModel.DataAnnotations;

namespace Restoran.ViewModels
{
    public class CustomerTableOptionViewModel
    {
        public int Id { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public TableStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public bool CanStartOrder { get; set; }
    }

    public class TableManagementViewModel
    {
        public IReadOnlyList<TableManagementItemViewModel> Tables { get; init; } = Array.Empty<TableManagementItemViewModel>();
    }

    public class TableManagementItemViewModel
    {
        public int Id { get; init; }
        public string TableNumber { get; init; } = string.Empty;
        public int Capacity { get; init; }
        public TableStatus Status { get; init; }
        public string StatusLabel { get; init; } = string.Empty;
        public bool HasActiveSession { get; init; }
        public DateTime? SessionStartedAt { get; init; }
        public string SessionCustomerType { get; init; } = string.Empty;
        public string SessionCustomerName { get; init; } = string.Empty;
        public int ActiveTransactionCount { get; init; }
        public string QrCodeUrl { get; init; } = string.Empty;
    }

    public class TableFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nomor meja wajib diisi")]
        [StringLength(10)]
        public string TableNumber { get; set; } = string.Empty;

        [Range(1, 20, ErrorMessage = "Kapasitas harus antara 1 sampai 20")]
        public int Capacity { get; set; }

        [Required]
        public TableStatus Status { get; set; } = TableStatus.Available;

        public string ExistingQrCodeUrl { get; set; } = string.Empty;

        public bool HasActiveSession { get; set; }
    }
}
