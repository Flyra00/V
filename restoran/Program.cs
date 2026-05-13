using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Infrastructure.Startup;
using Restoran.Shared.Extensions;
using Restoran.Shared.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.Configure<AppSettingsOptions>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection("Midtrans"));
builder.Services.AddHttpClient<Restoran.Features.Payments.Services.IMidtransService, Restoran.Features.Payments.Services.MidtransService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Login";
        options.LogoutPath = "/Home/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Configure DbContext with SQL Server
var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string belum diset. Isi ConnectionStrings:DefaultConnection di appsettings.Local.json " +
        "atau set environment variable ConnectionStrings__DefaultConnection.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));
builder.Services.AddRestoranModules();

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Index}/{id?}");

await app.InitializeDatabaseAsync();

app.Run();
