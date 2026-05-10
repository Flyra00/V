namespace Restoran.Features.Customer.Services
{
    public interface IQrCodeService
    {
        byte[] GenerateMenuQrCode(string url);
    }
}
