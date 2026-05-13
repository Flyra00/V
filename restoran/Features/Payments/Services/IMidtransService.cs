using Restoran.Models;
using Restoran.Shared.Results;
using System.Text.Json.Serialization;

namespace Restoran.Features.Payments.Services
{
    public interface IMidtransService
    {
        Task<MidtransSnapResponse> CreateSnapTransactionAsync(Transaction transaction, CancellationToken ct = default);
        Task<MidtransStatusResponse> GetTransactionStatusAsync(string midtransOrderId, CancellationToken ct = default);
        Task<OperationResult> ApplyStatusToPaymentAsync(int transactionId, CancellationToken ct = default);
        Task<OperationResult<MidtransPaymentUpdateResult>> ProcessNotificationAsync(MidtransNotificationRequest notification, CancellationToken ct = default);
    }

    public sealed class MidtransSnapResponse
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public string RedirectUrl { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;

        public static MidtransSnapResponse Success(string token, string redirectUrl, string rawJson)
            => new() { Succeeded = true, Token = token, RedirectUrl = redirectUrl, RawJson = rawJson };

        public static MidtransSnapResponse Failure(string message)
            => new() { Succeeded = false, Message = message };
    }

    public sealed class MidtransStatusResponse
    {
        public bool Succeeded { get; init; }
        public bool IsNotFound { get; init; }
        public string Message { get; init; } = string.Empty;
        public string OrderId { get; init; } = string.Empty;
        public string TransactionId { get; init; } = string.Empty;
        public string TransactionStatus { get; init; } = string.Empty;
        public string FraudStatus { get; init; } = string.Empty;
        public string PaymentType { get; init; } = string.Empty;
        public string GrossAmount { get; init; } = string.Empty;
        public string StatusCode { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;

        public static MidtransStatusResponse Success(
            string orderId,
            string transactionId,
            string transactionStatus,
            string fraudStatus,
            string paymentType,
            string grossAmount,
            string statusCode,
            string statusMessage,
            string rawJson)
            => new()
            {
                Succeeded = true,
                OrderId = orderId,
                TransactionId = transactionId,
                TransactionStatus = transactionStatus,
                FraudStatus = fraudStatus,
                PaymentType = paymentType,
                GrossAmount = grossAmount,
                StatusCode = statusCode,
                StatusMessage = statusMessage,
                RawJson = rawJson
            };

        public static MidtransStatusResponse NotFound(string message)
            => new() { Succeeded = false, IsNotFound = true, Message = message };

        public static MidtransStatusResponse Failure(string message)
            => new() { Succeeded = false, Message = message };
    }

    public sealed class MidtransNotificationRequest
    {
        [JsonPropertyName("transaction_time")]
        public string TransactionTime { get; init; } = string.Empty;

        [JsonPropertyName("transaction_status")]
        public string TransactionStatus { get; init; } = string.Empty;

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; init; } = string.Empty;

        [JsonPropertyName("status_message")]
        public string StatusMessage { get; init; } = string.Empty;

        [JsonPropertyName("status_code")]
        public string StatusCode { get; init; } = string.Empty;

        [JsonPropertyName("signature_key")]
        public string SignatureKey { get; init; } = string.Empty;

        [JsonPropertyName("payment_type")]
        public string PaymentType { get; init; } = string.Empty;

        [JsonPropertyName("order_id")]
        public string OrderId { get; init; } = string.Empty;

        [JsonPropertyName("gross_amount")]
        public string GrossAmount { get; init; } = string.Empty;

        [JsonPropertyName("fraud_status")]
        public string FraudStatus { get; init; } = string.Empty;
    }

    public sealed class MidtransPaymentUpdateResult
    {
        public int TransactionId { get; init; }
        public PaymentStatus PaymentStatus { get; init; }
        public string MidtransTransactionStatus { get; init; } = string.Empty;
    }
}
