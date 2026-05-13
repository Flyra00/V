using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restoran.Data;
using Restoran.Models;
using Restoran.Shared.Options;
using Restoran.Shared.Results;

namespace Restoran.Features.Payments.Services
{
    public class MidtransService : IMidtransService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MidtransService> _logger;
        private readonly MidtransOptions _options;

        public MidtransService(HttpClient httpClient, ApplicationDbContext context, IOptions<MidtransOptions> options, ILogger<MidtransService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<MidtransSnapResponse> CreateSnapTransactionAsync(Transaction transaction, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ServerKey))
            {
                return MidtransSnapResponse.Failure("ServerKey Midtrans Sandbox belum terbaca. Isi melalui user-secrets atau environment variable pada key Midtrans:ServerKey.");
            }

            var payment = transaction.Payment;
            if (payment == null || string.IsNullOrWhiteSpace(payment.MidtransOrderId))
            {
                return MidtransSnapResponse.Failure("Midtrans Order ID belum tersedia.");
            }

            var payload = new Dictionary<string, object?>
            {
                ["transaction_details"] = new
                {
                    order_id = payment.MidtransOrderId,
                    gross_amount = Convert.ToInt64(decimal.Round(transaction.Total, 0, MidpointRounding.AwayFromZero))
                },
                ["item_details"] = new[]
                {
                    new
                    {
                        id = transaction.TransactionNumber,
                        price = Convert.ToInt64(decimal.Round(transaction.Total, 0, MidpointRounding.AwayFromZero)),
                        quantity = 1,
                        name = "Total Tagihan Restoran"
                    }
                },
                ["customer_details"] = new
                {
                    first_name = string.IsNullOrWhiteSpace(transaction.CustomerName) ? "Pelanggan" : transaction.CustomerName
                }
            };

            if (!string.IsNullOrWhiteSpace(_options.FinishUrl) && !string.IsNullOrWhiteSpace(transaction.TrackingToken))
            {
                payload["callbacks"] = new
                {
                    finish = $"{_options.FinishUrl}?token={transaction.TrackingToken}"
                };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.SnapBaseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
            };
            ApplyBasicAuth(request);

            using var response = await _httpClient.SendAsync(request, ct);
            var rawJson = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return MidtransSnapResponse.Failure(BuildProviderErrorMessage("membuat transaksi", response.StatusCode, rawJson));
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;
                var token = root.TryGetProperty("token", out var tokenProperty) ? tokenProperty.GetString() ?? string.Empty : string.Empty;
                var redirectUrl = root.TryGetProperty("redirect_url", out var redirectProperty) ? redirectProperty.GetString() ?? string.Empty : string.Empty;

                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(redirectUrl))
                {
                    return MidtransSnapResponse.Failure("Midtrans Sandbox tidak mengembalikan token atau redirect URL.");
                }

                return MidtransSnapResponse.Success(token, redirectUrl, rawJson);
            }
            catch (JsonException)
            {
                return MidtransSnapResponse.Failure("Respons Midtrans Sandbox tidak valid saat membuat transaksi.");
            }
        }

        public async Task<MidtransStatusResponse> GetTransactionStatusAsync(string midtransOrderId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ServerKey))
            {
                return MidtransStatusResponse.Failure("ServerKey Midtrans Sandbox belum terbaca. Isi melalui user-secrets atau environment variable pada key Midtrans:ServerKey.");
            }

            if (string.IsNullOrWhiteSpace(midtransOrderId))
            {
                return MidtransStatusResponse.Failure("Midtrans Order ID belum tersedia.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.ApiBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(midtransOrderId)}/status");
            ApplyBasicAuth(request);

            using var response = await _httpClient.SendAsync(request, ct);
            var rawJson = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return MidtransStatusResponse.NotFound("Transaksi Midtrans belum dipilih/dibayar oleh pelanggan atau belum tersedia di Midtrans.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return MidtransStatusResponse.Failure(BuildProviderErrorMessage("mengambil status transaksi", response.StatusCode, rawJson));
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;

                return MidtransStatusResponse.Success(
                    root.TryGetProperty("order_id", out var orderIdProperty) ? orderIdProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("transaction_id", out var transactionIdProperty) ? transactionIdProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("transaction_status", out var statusProperty) ? statusProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("fraud_status", out var fraudProperty) ? fraudProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("payment_type", out var paymentTypeProperty) ? paymentTypeProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("gross_amount", out var grossAmountProperty) ? grossAmountProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("status_code", out var statusCodeProperty) ? statusCodeProperty.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("status_message", out var statusMessageProperty) ? statusMessageProperty.GetString() ?? string.Empty : string.Empty,
                    rawJson);
            }
            catch (JsonException)
            {
                return MidtransStatusResponse.Failure("Respons Midtrans Sandbox tidak valid saat mengambil status transaksi.");
            }
        }

        public async Task<OperationResult> ApplyStatusToPaymentAsync(int transactionId, CancellationToken ct = default)
        {
            var transaction = await _context.Transactions
                .Include(entity => entity.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, ct);

            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            var payment = transaction.Payment;
            if (payment == null)
            {
                return OperationResult.Failure("Data pembayaran tidak ditemukan");
            }

            if (payment.PaymentMethodOption.LegacyMethod == PaymentMethod.Tunai)
            {
                return OperationResult.Failure("Metode pembayaran tunai tidak menggunakan Midtrans.");
            }

            if (string.IsNullOrWhiteSpace(payment.MidtransOrderId))
            {
                return OperationResult.Failure("Transaksi Midtrans belum dipilih/dibayar oleh pelanggan atau belum tersedia di Midtrans.");
            }

            var statusResponse = await GetTransactionStatusAsync(payment.MidtransOrderId, ct);
            if (statusResponse.IsNotFound)
            {
                return OperationResult.Failure(statusResponse.Message);
            }

            if (!statusResponse.Succeeded)
            {
                return OperationResult.Failure(statusResponse.Message);
            }

            return await ApplyStatusToPaymentAsync(
                transaction,
                payment,
                new MidtransStatusSnapshot(
                    statusResponse.OrderId,
                    statusResponse.TransactionId,
                    statusResponse.TransactionStatus,
                    statusResponse.FraudStatus,
                    statusResponse.PaymentType,
                    statusResponse.GrossAmount,
                    statusResponse.StatusCode,
                    statusResponse.StatusMessage,
                    statusResponse.RawJson),
                ct);
        }

        public async Task<OperationResult<MidtransPaymentUpdateResult>> ProcessNotificationAsync(MidtransNotificationRequest notification, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ServerKey))
            {
                return OperationResult<MidtransPaymentUpdateResult>.Failure("ServerKey Midtrans Sandbox belum terbaca. Isi melalui user-secrets atau environment variable pada key Midtrans:ServerKey.");
            }

            if (string.IsNullOrWhiteSpace(notification.OrderId))
            {
                return OperationResult<MidtransPaymentUpdateResult>.Failure("Order ID Midtrans pada notifikasi tidak valid.");
            }

            if (!IsNotificationSignatureValid(notification))
            {
                _logger.LogWarning("Midtrans notification signature mismatch for order {OrderId}", notification.OrderId);
                return OperationResult<MidtransPaymentUpdateResult>.Failure("Signature Midtrans tidak valid.");
            }

            var transaction = await _context.Transactions
                .Include(entity => entity.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .FirstOrDefaultAsync(entity => entity.Payment != null && entity.Payment.MidtransOrderId == notification.OrderId, ct);

            if (transaction == null || transaction.Payment == null)
            {
                return OperationResult<MidtransPaymentUpdateResult>.NotFound("Transaksi Midtrans tidak ditemukan.");
            }

            if (transaction.Payment.PaymentMethodOption.LegacyMethod == PaymentMethod.Tunai)
            {
                return OperationResult<MidtransPaymentUpdateResult>.Failure("Notifikasi Midtrans tidak berlaku untuk pembayaran tunai.");
            }

            if (!IsGrossAmountValid(transaction.Total, notification.GrossAmount))
            {
                _logger.LogWarning(
                    "Midtrans notification gross amount mismatch for order {OrderId}. Expected {ExpectedTotal}, got {GrossAmount}",
                    notification.OrderId,
                    transaction.Total,
                    notification.GrossAmount);
                return OperationResult<MidtransPaymentUpdateResult>.Failure("Jumlah pembayaran Midtrans tidak sesuai dengan total transaksi lokal.");
            }

            var rawJson = JsonSerializer.Serialize(notification, SerializerOptions);
            var applyResult = await ApplyStatusToPaymentAsync(
                transaction,
                transaction.Payment,
                new MidtransStatusSnapshot(
                    notification.OrderId,
                    notification.TransactionId,
                    notification.TransactionStatus,
                    notification.FraudStatus,
                    notification.PaymentType,
                    notification.GrossAmount,
                    notification.StatusCode,
                    notification.StatusMessage,
                    rawJson),
                ct);

            if (!applyResult.Succeeded)
            {
                return OperationResult<MidtransPaymentUpdateResult>.Failure(applyResult.Message);
            }

            return OperationResult<MidtransPaymentUpdateResult>.Success(
                new MidtransPaymentUpdateResult
                {
                    TransactionId = transaction.Id,
                    PaymentStatus = transaction.Payment.PaymentStatus,
                    MidtransTransactionStatus = transaction.Payment.MidtransTransactionStatus
                },
                applyResult.Message);
        }

        private void ApplyBasicAuth(HttpRequestMessage request)
        {
            var credentialBytes = Encoding.UTF8.GetBytes($"{_options.ServerKey}:");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
        }

        private async Task<OperationResult> ApplyStatusToPaymentAsync(
            Transaction transaction,
            Payment payment,
            MidtransStatusSnapshot status,
            CancellationToken ct)
        {
            payment.MidtransTransactionId = status.TransactionId;
            payment.MidtransPaymentType = status.PaymentType;
            payment.MidtransTransactionStatus = status.TransactionStatus;
            payment.MidtransFraudStatus = status.FraudStatus;
            payment.ProviderResponseJson = status.RawJson;
            payment.UpdatedAt = DateTime.Now;

            var transactionStatus = status.TransactionStatus.Trim().ToLowerInvariant();
            var fraudStatus = status.FraudStatus.Trim().ToLowerInvariant();

            if (payment.PaymentStatus == PaymentStatus.Paid && !CanOverridePaidStatus(transactionStatus))
            {
                await _context.SaveChangesAsync(ct);
                return OperationResult.Success("Notifikasi Midtrans lama diabaikan karena pembayaran sudah lunas.");
            }

            switch (transactionStatus)
            {
                case "settlement":
                    MarkPaid(payment, transaction.Total);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success("Pembayaran Midtrans sudah lunas.");
                case "capture" when string.IsNullOrWhiteSpace(fraudStatus) || fraudStatus == "accept":
                    MarkPaid(payment, transaction.Total);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success("Pembayaran Midtrans sudah lunas.");
                case "capture" when fraudStatus == "challenge":
                case "authorize":
                case "pending":
                    MarkPending(payment);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success(string.IsNullOrWhiteSpace(status.StatusMessage)
                        ? "Pembayaran Midtrans masih menunggu."
                        : status.StatusMessage);
                case "cancel":
                    MarkCancelled(payment);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success("Pembayaran Midtrans dibatalkan.");
                case "expire":
                    MarkCancelled(payment);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success("Pembayaran Midtrans kedaluwarsa.");
                case "deny":
                case "failure":
                case "capture" when fraudStatus == "deny":
                    MarkFailed(payment);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success("Pembayaran Midtrans gagal.");
                case "refund":
                case "partial_refund":
                case "chargeback":
                case "partial_chargeback":
                    MarkFailed(payment);
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success("Pembayaran Midtrans dibatalkan atau dikembalikan dan perlu ditinjau.");
                default:
                    await _context.SaveChangesAsync(ct);
                    return OperationResult.Success(string.IsNullOrWhiteSpace(status.StatusMessage)
                        ? "Status Midtrans berhasil diperbarui."
                        : status.StatusMessage);
            }
        }

        private static void MarkPaid(Payment payment, decimal total)
        {
            payment.PaymentStatus = PaymentStatus.Paid;
            payment.PaymentDate ??= DateTime.Now;
            payment.Amount = total;
            payment.AmountReceived = total;
            payment.ChangeAmount = 0;
        }

        private static void MarkPending(Payment payment)
        {
            payment.PaymentStatus = PaymentStatus.Pending;
            payment.PaymentDate = null;
        }

        private static void MarkCancelled(Payment payment)
        {
            payment.PaymentStatus = PaymentStatus.Cancelled;
            payment.PaymentDate = null;
        }

        private static void MarkFailed(Payment payment)
        {
            payment.PaymentStatus = PaymentStatus.Failed;
            payment.PaymentDate = null;
        }

        private bool IsNotificationSignatureValid(MidtransNotificationRequest notification)
        {
            var signatureSource = string.Concat(notification.OrderId, notification.StatusCode, notification.GrossAmount, _options.ServerKey);
            var signatureBytes = SHA512.HashData(Encoding.UTF8.GetBytes(signatureSource));
            var providedSignature = notification.SignatureKey.Trim();
            if (string.IsNullOrWhiteSpace(providedSignature))
            {
                return false;
            }

            var expectedSignature = Convert.ToHexString(signatureBytes).ToLowerInvariant();
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
            var providedBytes = Encoding.UTF8.GetBytes(providedSignature.ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }

        private static bool CanOverridePaidStatus(string transactionStatus)
        {
            return transactionStatus is "refund" or "partial_refund" or "chargeback" or "partial_chargeback";
        }

        private static bool IsGrossAmountValid(decimal expectedTotal, string grossAmount)
        {
            if (!decimal.TryParse(grossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedGrossAmount))
            {
                return false;
            }

            return decimal.Round(parsedGrossAmount, 2, MidpointRounding.AwayFromZero) ==
                   decimal.Round(expectedTotal, 2, MidpointRounding.AwayFromZero);
        }

        private static string BuildProviderErrorMessage(string operation, HttpStatusCode statusCode, string rawJson)
        {
            var statusMessage = TryReadProviderStatusMessage(rawJson);
            var statusLabel = $"{(int)statusCode} ({statusCode})";
            return string.IsNullOrWhiteSpace(statusMessage)
                ? $"Gagal {operation} Midtrans Sandbox: {statusLabel}."
                : $"Gagal {operation} Midtrans Sandbox: {statusLabel} - {statusMessage}";
        }

        private static string TryReadProviderStatusMessage(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;
                if (root.TryGetProperty("status_message", out var statusMessageProperty))
                {
                    return statusMessageProperty.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("error_messages", out var errorMessagesProperty) &&
                    errorMessagesProperty.ValueKind == JsonValueKind.Array)
                {
                    return string.Join("; ", errorMessagesProperty.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value))!);
                }

                return string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private sealed record MidtransStatusSnapshot(
            string OrderId,
            string TransactionId,
            string TransactionStatus,
            string FraudStatus,
            string PaymentType,
            string GrossAmount,
            string StatusCode,
            string StatusMessage,
            string RawJson);
    }
}
