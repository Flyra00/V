using Microsoft.AspNetCore.Http;
using Restoran.Features.Customer.Services;

namespace Restoran.Infrastructure.Security
{
    public class CustomerOrderContextService : ICustomerOrderContextService
    {
        private const string ActiveTableCookieName = "ActiveTableId";
        private const string ActiveTrackingTokenCookieName = "ActiveTrackingToken";
        private const string LegacyActiveTransactionCookieName = "ActiveTransactionId";

        private static readonly CookieOptions DefaultCookieOptions = new()
        {
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        };

        public void SetActiveTableId(HttpResponse response, int tableId)
        {
            response.Cookies.Append(ActiveTableCookieName, tableId.ToString(), DefaultCookieOptions);
        }

        public int? GetActiveTableId(HttpRequest request)
        {
            return TryGetIntCookie(request, ActiveTableCookieName);
        }

        public void SetActiveTrackingToken(HttpResponse response, string trackingToken)
        {
            if (string.IsNullOrWhiteSpace(trackingToken))
            {
                response.Cookies.Delete(ActiveTrackingTokenCookieName, DefaultCookieOptions);
                response.Cookies.Delete(LegacyActiveTransactionCookieName, DefaultCookieOptions);
                return;
            }

            response.Cookies.Append(ActiveTrackingTokenCookieName, trackingToken, DefaultCookieOptions);
            response.Cookies.Delete(LegacyActiveTransactionCookieName, DefaultCookieOptions);
        }

        public string? GetActiveTrackingToken(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue(ActiveTrackingTokenCookieName, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        public void Clear(HttpResponse response)
        {
            response.Cookies.Delete(ActiveTableCookieName, DefaultCookieOptions);
            response.Cookies.Delete(ActiveTrackingTokenCookieName, DefaultCookieOptions);
            response.Cookies.Delete(LegacyActiveTransactionCookieName, DefaultCookieOptions);
        }

        private static int? TryGetIntCookie(HttpRequest request, string cookieName)
        {
            if (!request.Cookies.TryGetValue(cookieName, out var value))
            {
                return null;
            }

            return int.TryParse(value, out var parsedValue) ? parsedValue : null;
        }
    }
}
