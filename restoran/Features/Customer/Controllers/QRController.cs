using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Customer.Services;

namespace Restoran.Controllers
{
    public class QRController : Controller
    {
        private readonly IQrCodeService _qrCodeService;

        public QRController(IQrCodeService qrCodeService)
        {
            _qrCodeService = qrCodeService;
        }

        public IActionResult Generate(int tableId)
        {
            var url = $"{Request.Scheme}://{Request.Host}/Order/Menu?table={tableId}";
            return File(_qrCodeService.GenerateMenuQrCode(url), "image/png");
        }
    }
}
