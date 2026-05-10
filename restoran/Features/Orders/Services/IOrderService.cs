using Microsoft.AspNetCore.Http;
using Restoran.Features.Orders.Dtos;
using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Orders.Services
{
    public interface IOrderService
    {
        Task<OrderMenuViewModel?> GetMenuAsync(int tableId, CancellationToken cancellationToken = default);
        Task<OperationResult<CreateOrderResponse>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
        Task<OperationResult> UploadPaymentProofAsync(int transactionId, IFormFile paymentProof, CancellationToken cancellationToken = default);
        Task<Transaction?> GetConfirmationAsync(int transactionId, CancellationToken cancellationToken = default);
    }
}
