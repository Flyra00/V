using Microsoft.AspNetCore.Http;

namespace Restoran.Features.Customer.Services
{
    public interface ICustomerOrderContextService
    {
        void SetActiveTableId(HttpResponse response, int tableId);
        int? GetActiveTableId(HttpRequest request);
        void SetActiveTrackingToken(HttpResponse response, string trackingToken);
        string? GetActiveTrackingToken(HttpRequest request);
        void Clear(HttpResponse response);
    }
}
