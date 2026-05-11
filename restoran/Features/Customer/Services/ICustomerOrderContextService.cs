using Microsoft.AspNetCore.Http;

namespace Restoran.Features.Customer.Services
{
    public interface ICustomerOrderContextService
    {
        void SetActiveTableId(HttpResponse response, int tableId);
        int? GetActiveTableId(HttpRequest request);
        void SetActiveTransactionId(HttpResponse response, int transactionId);
        int? GetActiveTransactionId(HttpRequest request);
        void Clear(HttpResponse response);
    }
}
