using Restoran.Tests;

SQLitePCL.Batteries_V2.Init();

var tests = new (string Name, Func<Task> Run)[]
{
    ("Seed development profile is idempotent", SeedDataTests.InitializeAsync_SeedsDevelopmentDemoData_AndIsIdempotent),
    ("Seed production profile skips demo data", SeedDataTests.InitializeAsync_SkipsDemoData_OutsideDevelopment),
    ("Charge settings update persists active configuration", ChargeSettingsTests.AdminService_UpdateChargeSettingsAsync_PersistsChargeConfiguration),
    ("Charge configuration provider reads active settings", ChargeSettingsTests.ChargeConfigurationProvider_GetCurrentAsync_UsesActiveDatabaseSettings),
    ("Auth.Login success updates last login", AuthAndValidationTests.LoginStaffAsync_ReturnsSuccess_AndUpdatesLastLogin),
    ("Auth.Login failure rejects invalid password", AuthAndValidationTests.LoginStaffAsync_ReturnsFailure_WhenPasswordInvalid),
    ("Category duplicate validation", AuthAndValidationTests.CategoryService_CreateCategoryAsync_RejectsDuplicateName_CaseInsensitive),
    ("Admin duplicate username validation", AuthAndValidationTests.AdminService_CreateUserAsync_RejectsDuplicateUsername),
    ("Admin duplicate email validation", AuthAndValidationTests.AdminService_CreateUserAsync_RejectsDuplicateEmail),
    ("Order create computes totals and notification", OrderAndOperationsTests.CreateOrderAsync_ComputesTotals_Discount_AndNotification),
    ("Table session closes after served and paid", TableSessionTests.TableSession_Closes_WhenOrderServedAndPaymentPaid),
    ("Table duplicate number validation", TableSessionTests.TableService_CreateAsync_RejectsDuplicateTableNumber),
    ("Cashier confirm payment", OrderAndOperationsTests.ConfirmPaymentAsync_MarksTransactionAsPaid),
    ("Kitchen status propagation", OrderAndOperationsTests.UpdateStatusAsync_PropagatesStatusToOrderDetails),
    ("Asset log create deducts stock", OrderAndOperationsTests.AssetLogService_CreateAsync_DeductsStockAndCreatesReportedLog),
    ("Asset log approve sets approval fields", OrderAndOperationsTests.AssetLogService_ApproveAsync_SetsApprovalFields)
};

var failures = new List<string>();

foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex);
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"FAILED {failures.Count} test(s).");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine($"PASSED {tests.Length} test(s).");
return 0;
