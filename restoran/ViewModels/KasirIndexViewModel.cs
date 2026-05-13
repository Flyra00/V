using Restoran.Models;

namespace Restoran.ViewModels
{
    public class KasirIndexViewModel
    {
        public IReadOnlyList<Transaction> Transactions { get; set; } = Array.Empty<Transaction>();
        public IReadOnlyList<Promo> ActivePromos { get; set; } = Array.Empty<Promo>();
    }
}
