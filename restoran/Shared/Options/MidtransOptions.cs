namespace Restoran.Shared.Options
{
    public class MidtransOptions
    {
        public bool IsProduction { get; set; } = false;
        public string ServerKey { get; set; } = "";
        public string ClientKey { get; set; } = "";
        public string SnapBaseUrl { get; set; } = "https://app.sandbox.midtrans.com/snap/v1/transactions";
        public string ApiBaseUrl { get; set; } = "https://api.sandbox.midtrans.com/v2";
        public string FinishUrl { get; set; } = string.Empty;
    }
}
