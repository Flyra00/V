using Restoran.Features.Admin.Services;
using Restoran.Features.Auth.Services;
using Restoran.Features.Catalog.Services;
using Restoran.Features.Customer.Services;
using Restoran.Features.Dashboard.Services;
using Restoran.Features.Kasir.Services;
using Restoran.Features.Kitchen.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Payments.Services;
using Restoran.Features.Supervisor.Services;
using Restoran.Features.Tables.Services;
using Restoran.Infrastructure.Files;
using Restoran.Infrastructure.Persistence;
using Restoran.Infrastructure.Security;
using Restoran.Shared.Abstractions;

namespace Restoran.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRestoranModules(this IServiceCollection services)
        {
            services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
            services.AddScoped<ITransactionNumberGenerator, TransactionNumberGenerator>();
            services.AddScoped<IChargeConfigurationProvider, ChargeConfigurationProvider>();
            services.AddScoped<IPaymentProofStorage, PaymentProofStorage>();
            services.AddScoped<IProductImageStorage, ProductImageStorage>();
            services.AddScoped<IAuthCookieService, AuthCookieService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICustomerMenuService, CustomerMenuService>();
            services.AddScoped<ICustomerOrderContextService, CustomerOrderContextService>();
            services.AddScoped<IQrCodeService, QrCodeService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ICashierService, CashierService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IKitchenService, KitchenService>();
            services.AddScoped<ISupervisorService, SupervisorService>();
            services.AddScoped<IAssetLogService, AssetLogService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<ITableService, TableService>();

            return services;
        }
    }
}
