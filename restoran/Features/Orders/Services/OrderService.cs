using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Payments.Services;
using Restoran.Features.Orders.Dtos;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Orders.Services
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITransactionNumberGenerator _transactionNumberGenerator;
        private readonly IPaymentProofStorage _paymentProofStorage;
        private readonly IChargeConfigurationProvider _chargeConfigurationProvider;
        private readonly ITableService _tableService;
        private readonly IPaymentService _paymentService;
        private readonly IMidtransService _midtransService;

        public OrderService(
            ApplicationDbContext context,
            IDateTimeProvider dateTimeProvider,
            ITransactionNumberGenerator transactionNumberGenerator,
            IPaymentProofStorage paymentProofStorage,
            IChargeConfigurationProvider chargeConfigurationProvider,
            ITableService tableService,
            IPaymentService paymentService,
            IMidtransService midtransService)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _transactionNumberGenerator = transactionNumberGenerator;
            _paymentProofStorage = paymentProofStorage;
            _chargeConfigurationProvider = chargeConfigurationProvider;
            _tableService = tableService;
            _paymentService = paymentService;
            _midtransService = midtransService;
        }

        public async Task<OrderMenuViewModel?> GetMenuAsync(int tableId, CancellationToken cancellationToken = default)
        {
            if (!await _tableService.CanStartOrderAsync(tableId, cancellationToken))
            {
                return null;
            }

            var table = await _context.Tables
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken);

            if (table == null)
            {
                return null;
            }

            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
            var now = _dateTimeProvider.Now;
            var activePromosRaw = await _context.Promos
                .AsNoTracking()
                .Where(promo => promo.IsActive && promo.StartsAt <= now && promo.EndsAt >= now)
                .ToListAsync(cancellationToken);
            var activePromos = activePromosRaw
                .OrderBy(promo => promo.MinimumPurchase)
                .ThenByDescending(promo => promo.DiscountValue)
                .ToList();
            var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);
            var paymentMethods = await _paymentService.GetActiveCustomerMethodsAsync(cancellationToken);

            return new OrderMenuViewModel
            {
                TableId = tableId,
                TableNumber = table.TableNumber,
                TaxName = chargeConfiguration.TaxName,
                TaxRate = chargeConfiguration.TaxRate,
                ServiceChargeName = chargeConfiguration.ServiceChargeName,
                ServiceChargeRate = chargeConfiguration.ServiceChargeRate,
                ActivePromos = activePromos
                    .Select(promo => new PromoSummaryViewModel
                    {
                        Name = promo.Name,
                        DiscountPercentage = promo.DiscountValue,
                        MinimumPurchase = promo.MinimumPurchase,
                        EndsAt = promo.EndsAt
                    })
                    .ToList(),
                PaymentMethods = BuildCustomerPaymentMethods(paymentMethods),
                ProductsByCategory = products
                    .GroupBy(p => p.Category.Name)
                    .ToDictionary(group => group.Key, group => group.ToList())
            };
        }

        public async Task<OperationResult<CreateOrderResponse>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return OperationResult<CreateOrderResponse>.Failure("Pesanan tidak boleh kosong");
            }

            if (!await _tableService.CanStartOrderAsync(request.TableId, cancellationToken))
            {
                return OperationResult<CreateOrderResponse>.Failure("Meja ini sedang tidak tersedia.");
            }

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == request.TableId, cancellationToken);
            if (table == null)
            {
                return OperationResult<CreateOrderResponse>.NotFound("Meja tidak ditemukan");
            }

            var now = _dateTimeProvider.Now;
            var transactionNumber = await _transactionNumberGenerator.GenerateAsync(cancellationToken);
            if (request.PaymentMethod != PaymentMethod.Tunai)
            {
                return OperationResult<CreateOrderResponse>.Failure("Pembayaran online sedang dinonaktifkan sementara. Silakan gunakan pembayaran tunai.");
            }

            var availablePaymentMethod = (await _paymentService.GetActiveCustomerMethodsAsync(cancellationToken))
                .Any(method => method.LegacyMethod == request.PaymentMethod);
            if (!availablePaymentMethod)
            {
                return OperationResult<CreateOrderResponse>.Failure("Metode pembayaran tidak tersedia");
            }

            var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            OperationResult<CreateOrderResponse> operationResult = OperationResult<CreateOrderResponse>.Failure("Pesanan gagal diproses");

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var resolvedMember = await ResolveMemberForOrderAsync(request, cancellationToken);

                    var activeSession = await _tableService.EnsureActiveSessionAsync(
                        request.TableId,
                        request.IsMember ? CustomerType.Member : CustomerType.Guest,
                        resolvedMember?.Id,
                        request.CustomerName,
                        cancellationToken);

                var transaction = new Transaction
                {
                    TransactionNumber = transactionNumber,
                    TrackingToken = await GenerateTrackingTokenAsync(cancellationToken),
                    TableId = request.TableId,
                    TableSessionId = activeSession.Id,
                    CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Guest" : request.CustomerName,
                    CustomerType = request.IsMember ? CustomerType.Member : CustomerType.Guest,
                    OrderStatus = OrderStatus.New,
                    CreatedAt = now
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                decimal subtotal = 0;
                foreach (var item in request.Items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        continue;
                    }

                    _context.TransactionDetails.Add(new TransactionDetail
                    {
                        TransactionId = transaction.Id,
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price,
                        Notes = item.Notes,
                        Status = DetailStatus.Pending,
                        CreatedAt = now
                    });

                    subtotal += item.Quantity * product.Price;
                }

                    if (subtotal <= 0)
                    {
                        operationResult = OperationResult<CreateOrderResponse>.Failure("Item pesanan tidak valid");
                        await dbTransaction.RollbackAsync(cancellationToken);
                        return;
                    }

                transaction.Subtotal = subtotal;
                var chargeConfiguration = await _chargeConfigurationProvider.GetCurrentAsync(cancellationToken);
                transaction.Tax = chargeConfiguration.CalculateTax(subtotal);
                transaction.ServiceCharge = chargeConfiguration.CalculateServiceCharge(subtotal);

                    var memberDiscount = CalculateMemberDiscountAsync(request, products, resolvedMember);
                var promoSelection = await SelectBestPromoAsync(subtotal, memberDiscount, now, cancellationToken);

                transaction.Discount = memberDiscount + promoSelection.DiscountAmount;
                transaction.PromoId = promoSelection.Promo?.Id;
                transaction.AppliedPromoName = promoSelection.Promo?.Name ?? string.Empty;

                transaction.Total = transaction.Subtotal + transaction.Tax + transaction.ServiceCharge - transaction.Discount;

                var paymentResult = await _paymentService.CreateOrSyncPaymentAsync(
                    transaction,
                    transaction.Total,
                    request.PaymentMethod,
                    PaymentStatus.Pending,
                    now,
                    string.Empty,
                    cancellationToken);

                    if (!paymentResult.Succeeded)
                    {
                        operationResult = OperationResult<CreateOrderResponse>.Failure(paymentResult.Message);
                        await dbTransaction.RollbackAsync(cancellationToken);
                        return;
                    }

                MidtransSnapResponse? snapResponse = null;
                var isMidtransPayment = IsMidtransPaymentMethod(request.PaymentMethod);
                if (isMidtransPayment)
                {
                    paymentResult.Data!.MidtransOrderId = $"RST-{transaction.TransactionNumber}-{transaction.Id}";
                    await _context.SaveChangesAsync(cancellationToken);

                    transaction.Payment = paymentResult.Data;
                    snapResponse = await _midtransService.CreateSnapTransactionAsync(transaction, cancellationToken);
                        if (!snapResponse.Succeeded)
                        {
                            operationResult = OperationResult<CreateOrderResponse>.Failure(snapResponse.Message);
                            await dbTransaction.RollbackAsync(cancellationToken);
                            return;
                        }

                    paymentResult.Data.SnapToken = snapResponse.Token;
                    paymentResult.Data.SnapRedirectUrl = snapResponse.RedirectUrl;
                    paymentResult.Data.ProviderResponseJson = snapResponse.RawJson;
                    paymentResult.Data.MidtransTransactionStatus = "pending";
                    paymentResult.Data.UpdatedAt = now;
                }

                _context.Notifications.Add(new Notification
                {
                    TransactionId = transaction.Id,
                    Type = NotificationType.NewOrder,
                    Message = $"Pesanan baru dari Meja {table.TableNumber} - {transaction.TransactionNumber}",
                    Recipient = "BagianMasak",
                    CreatedAt = now
                });

                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);

                    operationResult = OperationResult<CreateOrderResponse>.Success(new CreateOrderResponse
                    {
                        TransactionId = transaction.Id,
                        TrackingToken = transaction.TrackingToken ?? string.Empty,
                        TransactionNumber = transaction.TransactionNumber,
                        AppliedPromoName = transaction.AppliedPromoName,
                        DiscountAmount = transaction.Discount,
                        IsMidtransPayment = isMidtransPayment,
                        PaymentRedirectUrl = snapResponse?.RedirectUrl ?? string.Empty,
                        SnapToken = snapResponse?.Token ?? string.Empty
                    });
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    operationResult = OperationResult<CreateOrderResponse>.Failure(GetDetailedErrorMessage(ex));
                }
            });

            return operationResult;
        }

        public async Task<OperationResult> UploadPaymentProofAsync(int transactionId, IFormFile paymentProof, CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions.FindAsync([transactionId], cancellationToken);
            if (transaction == null)
            {
                return OperationResult.NotFound("Transaksi tidak ditemukan");
            }

            if (paymentProof == null || paymentProof.Length == 0)
            {
                return OperationResult.Failure("File tidak valid");
            }

            try
            {
                var proofUrl = await _paymentProofStorage.SaveAsync(
                    transaction.TransactionNumber,
                    paymentProof,
                    cancellationToken);
                return await _paymentService.UpdatePaymentProofAsync(transactionId, proofUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(GetDetailedErrorMessage(ex));
            }
        }

        private static string GetDetailedErrorMessage(Exception ex)
        {
            var root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return string.Equals(root.Message, ex.Message, StringComparison.Ordinal)
                ? ex.Message
                : $"{ex.Message} | Root cause: {root.Message}";
        }

        public async Task<OperationResult> UploadPaymentProofByTrackingTokenAsync(string trackingToken, IFormFile paymentProof, CancellationToken cancellationToken = default)
        {
            var normalizedTrackingToken = NormalizeTrackingToken(trackingToken);
            if (string.IsNullOrWhiteSpace(normalizedTrackingToken))
            {
                return OperationResult.NotFound("Tracking pesanan tidak ditemukan");
            }

            var transactionId = await _context.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.TrackingToken == normalizedTrackingToken)
                .Select(transaction => (int?)transaction.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!transactionId.HasValue)
            {
                return OperationResult.NotFound("Tracking pesanan tidak ditemukan");
            }

            return await UploadPaymentProofAsync(transactionId.Value, paymentProof, cancellationToken);
        }

        public async Task<Transaction?> GetConfirmationAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Table)
                .Include(t => t.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        }

        public async Task<int?> ResolveTrackingTransactionIdAsync(
            int? activeTransactionId,
            int? activeTableId,
            int? memberUserId,
            CancellationToken cancellationToken = default)
        {
            if (activeTransactionId.HasValue &&
                await _context.Transactions.AnyAsync(transaction => transaction.Id == activeTransactionId.Value, cancellationToken))
            {
                return activeTransactionId.Value;
            }

            return await _context.Transactions
                .AsNoTracking()
                .Where(transaction =>
                    (activeTableId.HasValue && transaction.TableId == activeTableId.Value) ||
                    (memberUserId.HasValue &&
                     transaction.TableSession != null &&
                     transaction.TableSession.Member != null &&
                     transaction.TableSession.Member.UserId == memberUserId.Value))
                .OrderByDescending(transaction => transaction.CreatedAt)
                .Select(transaction => (int?)transaction.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<string?> ResolveTrackingTokenAsync(
            string? trackingToken,
            int? memberUserId,
            CancellationToken cancellationToken = default)
        {
            var normalizedTrackingToken = NormalizeTrackingToken(trackingToken);
            if (!string.IsNullOrWhiteSpace(normalizedTrackingToken))
            {
                var matchedToken = await _context.Transactions
                    .AsNoTracking()
                    .Where(transaction => transaction.TrackingToken == normalizedTrackingToken)
                    .Select(transaction => transaction.TrackingToken)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(matchedToken))
                {
                    return matchedToken;
                }
            }

            if (!memberUserId.HasValue)
            {
                return null;
            }

            return await _context.Transactions
                .AsNoTracking()
                .Where(transaction =>
                    !string.IsNullOrWhiteSpace(transaction.TrackingToken) &&
                    transaction.TableSession != null &&
                    transaction.TableSession.Member != null &&
                    transaction.TableSession.Member.UserId == memberUserId.Value)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .Select(transaction => transaction.TrackingToken)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<OrderTrackingViewModel?> GetTrackingAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            var transaction = await GetTrackingTransactionQuery()
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            return transaction == null ? null : MapToTrackingViewModel(transaction);
        }

        public async Task<OrderTrackingViewModel?> GetTrackingByTokenAsync(string trackingToken, CancellationToken cancellationToken = default)
        {
            var normalizedTrackingToken = NormalizeTrackingToken(trackingToken);
            if (string.IsNullOrWhiteSpace(normalizedTrackingToken))
            {
                return null;
            }

            var transaction = await GetTrackingTransactionQuery()
                .FirstOrDefaultAsync(entity => entity.TrackingToken == normalizedTrackingToken, cancellationToken);

            return transaction == null ? null : MapToTrackingViewModel(transaction);
        }

        public async Task<OrderTrackingStatusResponse?> GetTrackingStatusAsync(int transactionId, CancellationToken cancellationToken = default)
        {
            var transaction = await GetTrackingTransactionQuery()
                .FirstOrDefaultAsync(entity => entity.Id == transactionId, cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            return new OrderTrackingStatusResponse
            {
                TransactionId = transaction.Id,
                TrackingToken = transaction.TrackingToken ?? string.Empty,
                OrderStatus = transaction.OrderStatus,
                PaymentStatus = transaction.Payment!.PaymentStatus,
                MidtransTransactionStatus = transaction.Payment.MidtransTransactionStatus,
                PaidAt = transaction.Payment.PaymentDate,
                IsTrackingFinal = IsTrackingFinal(transaction.OrderStatus, transaction.Payment.PaymentStatus),
                RefreshedAt = _dateTimeProvider.Now,
                Items = transaction.TransactionDetails
                    .OrderBy(detail => detail.Id)
                    .Select(detail => new OrderTrackingItemViewModel
                    {
                        DetailId = detail.Id,
                        ProductName = detail.Product.Name,
                        Quantity = detail.Quantity,
                        Notes = detail.Notes,
                        Status = detail.Status
                    })
                    .ToList()
            };
        }

        public async Task<OrderTrackingStatusResponse?> GetTrackingStatusByTokenAsync(string trackingToken, CancellationToken cancellationToken = default)
        {
            var normalizedTrackingToken = NormalizeTrackingToken(trackingToken);
            if (string.IsNullOrWhiteSpace(normalizedTrackingToken))
            {
                return null;
            }

            var transaction = await GetTrackingTransactionQuery()
                .FirstOrDefaultAsync(entity => entity.TrackingToken == normalizedTrackingToken, cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            return new OrderTrackingStatusResponse
            {
                TransactionId = transaction.Id,
                TrackingToken = transaction.TrackingToken ?? string.Empty,
                OrderStatus = transaction.OrderStatus,
                PaymentStatus = transaction.Payment!.PaymentStatus,
                MidtransTransactionStatus = transaction.Payment.MidtransTransactionStatus,
                PaidAt = transaction.Payment.PaymentDate,
                IsTrackingFinal = IsTrackingFinal(transaction.OrderStatus, transaction.Payment.PaymentStatus),
                RefreshedAt = _dateTimeProvider.Now,
                Items = transaction.TransactionDetails
                    .OrderBy(detail => detail.Id)
                    .Select(detail => new OrderTrackingItemViewModel
                    {
                        DetailId = detail.Id,
                        ProductName = detail.Product.Name,
                        Quantity = detail.Quantity,
                        Notes = detail.Notes,
                        Status = detail.Status
                    })
                    .ToList()
            };
        }

        private static decimal CalculateMemberDiscountAsync(
            CreateOrderRequest request,
            IReadOnlyDictionary<int, Product> products,
            Member? resolvedMember)
        {
            if (!request.IsMember)
            {
                return 0m;
            }

            if (resolvedMember == null)
            {
                return 0m;
            }

            decimal totalMemberItemDiscount = 0m;
            foreach (var item in request.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product) || item.Quantity <= 0)
                {
                    continue;
                }

                if (product.MemberDiscountPercentage <= 0m)
                {
                    continue;
                }

                var lineSubtotal = product.Price * item.Quantity;
                totalMemberItemDiscount += lineSubtotal * (product.MemberDiscountPercentage / 100m);
            }

            return totalMemberItemDiscount;
        }

        private async Task<Member?> ResolveMemberForOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
        {
            if (!request.IsMember || !request.MemberId.HasValue)
            {
                return null;
            }

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value, cancellationToken);

            if (member != null)
            {
                return member;
            }

            // Backward compatibility: beberapa flow lama masih mengirim UserId.
            return await _context.Members
                .FirstOrDefaultAsync(m => m.UserId == request.MemberId.Value, cancellationToken);
        }

        private async Task<PromoSelection> SelectBestPromoAsync(
            decimal subtotal,
            decimal memberDiscount,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var discountedBase = Math.Max(0m, subtotal - memberDiscount);
            var eligiblePromos = await _context.Promos
                .Where(promo =>
                    promo.IsActive &&
                    promo.PromoType == PromoType.Percentage &&
                    promo.StartsAt <= now &&
                    promo.EndsAt >= now &&
                    promo.MinimumPurchase <= subtotal)
                .ToListAsync(cancellationToken);

            Promo? bestPromo = null;
            decimal bestDiscount = 0m;

            foreach (var promo in eligiblePromos)
            {
                var discountAmount = discountedBase * (promo.DiscountValue / 100);
                if (discountAmount > bestDiscount)
                {
                    bestPromo = promo;
                    bestDiscount = discountAmount;
                }
            }

            return new PromoSelection(bestPromo, bestDiscount);
        }

        private sealed record PromoSelection(Promo? Promo, decimal DiscountAmount);

        private IQueryable<Transaction> GetTrackingTransactionQuery()
        {
            return _context.Transactions
                .AsNoTracking()
                .Include(transaction => transaction.Table)
                .Include(transaction => transaction.TableSession!)
                    .ThenInclude(session => session.Member)
                        .ThenInclude(member => member!.User)
                .Include(transaction => transaction.Payment!)
                    .ThenInclude(payment => payment.PaymentMethodOption)
                .Include(transaction => transaction.TransactionDetails)
                    .ThenInclude(detail => detail.Product);
        }

        private static OrderTrackingViewModel MapToTrackingViewModel(Transaction transaction)
        {
            return new OrderTrackingViewModel
            {
                TransactionId = transaction.Id,
                TrackingToken = transaction.TrackingToken ?? string.Empty,
                TransactionNumber = transaction.TransactionNumber,
                TableId = transaction.TableId,
                TableNumber = transaction.Table?.TableNumber ?? "Takeaway",
                CreatedAt = transaction.CreatedAt,
                OrderStatus = transaction.OrderStatus,
                PaymentMethod = transaction.Payment!.PaymentMethodOption.LegacyMethod,
                PaymentMethodDisplayName = GetPaymentDisplayName(transaction.Payment.PaymentMethodOption.LegacyMethod, transaction.Payment.PaymentMethodOption.DisplayName),
                PaymentStatus = transaction.Payment.PaymentStatus,
                MidtransTransactionStatus = transaction.Payment.MidtransTransactionStatus,
                MidtransPaymentType = transaction.Payment.MidtransPaymentType,
                MidtransTransactionId = transaction.Payment.MidtransTransactionId,
                SnapRedirectUrl = transaction.Payment.SnapRedirectUrl,
                PaidAt = transaction.Payment.PaymentDate,
                Subtotal = transaction.Subtotal,
                Discount = transaction.Discount,
                Tax = transaction.Tax,
                ServiceCharge = transaction.ServiceCharge,
                Total = transaction.Total,
                AppliedPromoName = transaction.AppliedPromoName,
                PaymentProofUrl = transaction.Payment.ProofUrl,
                Items = transaction.TransactionDetails
                    .OrderBy(detail => detail.Id)
                    .Select(detail => new OrderTrackingItemViewModel
                    {
                        DetailId = detail.Id,
                        ProductName = detail.Product.Name,
                        Quantity = detail.Quantity,
                        Notes = detail.Notes,
                        Status = detail.Status
                    })
                    .ToList()
            };
        }

        private static bool IsTrackingFinal(OrderStatus orderStatus, PaymentStatus paymentStatus)
        {
            return orderStatus is OrderStatus.Completed or OrderStatus.Cancelled ||
                   (orderStatus == OrderStatus.Served && paymentStatus is PaymentStatus.Paid or PaymentStatus.Cancelled);
        }

        private static bool IsMidtransPaymentMethod(PaymentMethod paymentMethod)
            => paymentMethod is PaymentMethod.QRIS or PaymentMethod.Transfer;

        private static string GetPaymentDisplayName(PaymentMethod paymentMethod, string currentDisplayName)
            => IsMidtransPaymentMethod(paymentMethod)
                ? "Bayar Online"
                : currentDisplayName;

        private static IReadOnlyList<PaymentMethodSelectionViewModel> BuildCustomerPaymentMethods(IReadOnlyList<PaymentMethodOption> paymentMethods)
        {
            var result = new List<PaymentMethodSelectionViewModel>();
            
            foreach (var method in paymentMethods)
            {
                if (method.LegacyMethod != PaymentMethod.Tunai)
                {
                    continue;
                }

                result.Add(new PaymentMethodSelectionViewModel
                {
                    Id = method.Id,
                    Code = method.Code,
                    DisplayName = method.DisplayName,
                    LegacyMethod = method.LegacyMethod,
                    IsCustomerFacing = method.IsCustomerFacing,
                    IsCashierFacing = method.IsCashierFacing
                });
            }

            return result;
        }

        private async Task<string> GenerateTrackingTokenAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                var exists = await _context.Transactions
                    .AsNoTracking()
                    .AnyAsync(transaction => transaction.TrackingToken == token, cancellationToken);

                if (!exists)
                {
                    return token;
                }
            }
        }

        private static string? NormalizeTrackingToken(string? trackingToken)
        {
            return string.IsNullOrWhiteSpace(trackingToken)
                ? null
                : trackingToken.Trim().ToLowerInvariant();
        }
    }
}
