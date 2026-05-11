using Microsoft.AspNetCore.Http;
using Restoran.Features.Customer.Services;

namespace Restoran.Infrastructure.Security
{
    public class CustomerOrderContextService : ICustomerOrderContextService
    {
        private const string ActiveTableCookieName = "ActiveTableId";
        private const string ActiveTransactionCookieName = "ActiveTransactionId";

        private static readonly CookieOptions DefaultCookieOptions = new()
        {
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        };

        public void SetActiveTableId(HttpResponse response, int tableId)
        {
            response.Cookies.Append(ActiveTableCookieName, tableId.ToString(), DefaultCookieOptions);
        }

        public int? GetActiveTableId(HttpRequest request)
        {
            return TryGetIntCookie(request, ActiveTableCookieName);
        }

        public void SetActiveTransactionId(HttpResponse response, int transactionId)
        {
            response.Cookies.Append(ActiveTransactionCookieName, transactionId.ToString(), DefaultCookieOptions);
        }

        public int? GetActiveTransactionId(HttpRequest request)
        {
            return TryGetIntCookie(request, ActiveTransactionCookieName);
        }

        public void Clear(HttpResponse response)
        {
            response.Cookies.Delete(ActiveTableCookieName);
            response.Cookies.Delete(ActiveTransactionCookieName);
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
