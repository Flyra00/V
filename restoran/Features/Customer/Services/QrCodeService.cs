using QRCoder;

namespace Restoran.Features.Customer.Services
{
    public class QrCodeService : IQrCodeService
    {
        public byte[] GenerateMenuQrCode(string url)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }
    }
}
