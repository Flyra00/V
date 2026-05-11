using Restoran.Tests;

SQLitePCL.Batteries_V2.Init();

var tests = new (string Name, Func<Task> Run)[]
{
    ("Seed development profile is idempotent", SeedDataTests.InitializeAsync_SeedsDevelopmentDemoData_AndIsIdempotent),
    ("Seed production profile skips demo data", SeedDataTests.InitializeAsync_SkipsDemoData_OutsideDevelopment),
    ("Schema cleanup removes ingredient tables", DashboardInventoryTests.Schema_DoesNotCreateIngredientTables_AfterCleanup),
    ("Dashboard stock report is asset-centric", DashboardInventoryTests.GetStockReportAsync_ReturnsAssetsAndProductsOnly),
    ("Charge settings update persists active configuration", ChargeSettingsTests.AdminService_UpdateChargeSettingsAsync_PersistsChargeConfiguration),
    ("Charge configuration provider reads active settings", ChargeSettingsTests.ChargeConfigurationProvider_GetCurrentAsync_UsesActiveDatabaseSettings),
    ("Auth.Login success updates last login", AuthAndValidationTests.LoginStaffAsync_ReturnsSuccess_AndUpdatesLastLogin),
    ("Auth.Login failure rejects invalid password", AuthAndValidationTests.LoginStaffAsync_ReturnsFailure_WhenPasswordInvalid),
    ("Role seed backfills user role ids", RoleManagementTests.SeedData_BackfillsUserRoleId_FromSystemRoles),
    ("System role code change is rejected", RoleManagementTests.AdminService_UpdateRoleAsync_RejectsSystemRoleCodeChange),
    ("Admin user update syncs role bridge", RoleManagementTests.AdminService_UpdateUserAsync_SyncsRoleId_AndLegacyRuntimeRole),
    ("Auth login uses role bridge", RoleManagementTests.AuthService_LoginStaffAsync_UsesRoleEntityBridge_ForSessionAndRedirect),
    ("Category duplicate validation", AuthAndValidationTests.CategoryService_CreateCategoryAsync_RejectsDuplicateName_CaseInsensitive),
    ("Admin duplicate username validation", AuthAndValidationTests.AdminService_CreateUserAsync_RejectsDuplicateUsername),
    ("Admin duplicate email validation", AuthAndValidationTests.AdminService_CreateUserAsync_RejectsDuplicateEmail),
    ("Order create computes totals and notification", OrderAndOperationsTests.CreateOrderAsync_ComputesTotals_Discount_AndNotification),
    ("Tracking resolves active transaction", CustomerTrackingTests.ResolveTrackingTransactionIdAsync_PrefersActiveTransaction),
    ("Tracking falls back to latest member transaction", CustomerTrackingTests.ResolveTrackingTransactionIdAsync_FallsBackToLatestMemberTransaction),
    ("Tracking status reflects kitchen updates", CustomerTrackingTests.GetTrackingStatusAsync_ReflectsKitchenStatusUpdates),
    ("Tracking summary returns payment and promo data", CustomerTrackingTests.GetTrackingAsync_ReturnsPaymentAndPromoSummary),
    ("Promo migration applies to SQLite", PromoTests.Migration_ApplyPromoSchema_ToSqlite),
    ("Payment migration applies to SQLite", PaymentManagementTests.Migration_ApplyPaymentSchema_ToSqlite),
    ("Order create writes payment record", PaymentManagementTests.CreateOrderAsync_CreatesPaymentRecord),
    ("POS cash order writes paid payment", PaymentManagementTests.CreatePosOrderAsync_CreatesPaidCashPayment),
    ("Admin payment method update persists changes", PaymentManagementTests.AdminService_UpdatePaymentMethodAsync_PersistsChanges),
    ("Promo applies best active discount", PromoTests.CreateOrderAsync_AppliesBestEligiblePromo),
    ("Promo stacks after member discount", PromoTests.CreateOrderAsync_StacksMemberAndPromoDiscount),
    ("Promo admin validation rejects bad period", PromoTests.AdminService_CreatePromoAsync_RejectsInvalidPeriod),
    ("Promo admin update persists changes", PromoTests.AdminService_UpdatePromoAsync_PersistsChanges),
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
