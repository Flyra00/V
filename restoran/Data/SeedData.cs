using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Restoran.Models;

namespace Restoran.Data
{
    public static class SeedData
    {
        public static async Task Initialize(
            IServiceProvider serviceProvider,
            IHostEnvironment hostEnvironment,
            CancellationToken cancellationToken = default)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            await SeedMasterDataAsync(context, cancellationToken);

            if (!hostEnvironment.IsDevelopment())
            {
                return;
            }

            await SeedDemoDataAsync(context, cancellationToken);
        }

        private static async Task SeedMasterDataAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;

            await SeedCategoriesAsync(context, now, cancellationToken);
            await SeedChargeSettingsAsync(context, now, cancellationToken);
            await SeedTablesAsync(context, now, cancellationToken);
            await SeedProductsAsync(context, now, cancellationToken);
            await SeedIngredientsAsync(context, now, cancellationToken);
            await SeedAssetsAsync(context, now, cancellationToken);
        }

        private static async Task SeedDemoDataAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;

            await SeedUsersAsync(context, now, cancellationToken);
            await SeedMembersAsync(context, now, cancellationToken);
            await SeedDemoTransactionsAsync(context, now, cancellationToken);
            await SeedDemoAssetLogsAsync(context, now, cancellationToken);
        }

        private static async Task SeedCategoriesAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var definitions = new[]
            {
                new CategorySeed("Makanan", "Menu makanan utama"),
                new CategorySeed("Minuman", "Menu minuman"),
                new CategorySeed("Snack", "Menu camilan"),
                new CategorySeed("Dessert", "Menu pencuci mulut")
            };

            var existing = await context.Categories
                .ToDictionaryAsync(category => category.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var hasChanges = false;
            foreach (var definition in definitions)
            {
                if (existing.TryGetValue(definition.Name, out var category))
                {
                    if (category.Description != definition.Description)
                    {
                        category.Description = definition.Description;
                        hasChanges = true;
                    }

                    continue;
                }

                context.Categories.Add(new Category
                {
                    Name = definition.Name,
                    Description = definition.Description,
                    CreatedAt = now
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedUsersAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var definitions = new[]
            {
                new UserSeed("admin_V", "admin@restoran.com", "admin123", UserRole.Admin, true),
                new UserSeed("owner", "owner@restoran.com", "owner123", UserRole.Owner, true),
                new UserSeed("supervisor", "supervisor@restoran.com", "supervisor123", UserRole.Supervisor, true),
                new UserSeed("kasir", "kasir@restoran.com", "kasir123", UserRole.Kasir, true),
                new UserSeed("kitchen", "kitchen@restoran.com", "kitchen123", UserRole.BagianMasak, true),
                new UserSeed("member.regular", "member.regular@restoran.com", "member123", UserRole.Member, true),
                new UserSeed("member.silver", "member.silver@restoran.com", "member123", UserRole.Member, true),
                new UserSeed("member.gold", "member.gold@restoran.com", "member123", UserRole.Member, true),
                new UserSeed("member.platinum", "member.platinum@restoran.com", "member123", UserRole.Member, true),
                new UserSeed("kasir.cadangan", "kasir.cadangan@restoran.com", "kasircad123", UserRole.Kasir, false)
            };

            var users = await context.Users.ToListAsync(cancellationToken);
            var byUsername = users.ToDictionary(user => user.Username, StringComparer.OrdinalIgnoreCase);
            var byEmail = users.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);

            var hasChanges = false;
            foreach (var definition in definitions)
            {
                if (!byUsername.TryGetValue(definition.Username, out var user) &&
                    !byEmail.TryGetValue(definition.Email, out user))
                {
                    user = new User
                    {
                        Username = definition.Username,
                        Email = definition.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(definition.Password),
                        Role = definition.Role,
                        IsActive = definition.IsActive,
                        CreatedAt = now
                    };

                    context.Users.Add(user);
                    hasChanges = true;
                    continue;
                }

                if (user.Email != definition.Email)
                {
                    user.Email = definition.Email;
                    hasChanges = true;
                }

                if (user.Role != definition.Role)
                {
                    user.Role = definition.Role;
                    hasChanges = true;
                }

                if (user.IsActive != definition.IsActive)
                {
                    user.IsActive = definition.IsActive;
                    hasChanges = true;
                }

                if (!BCrypt.Net.BCrypt.Verify(definition.Password, user.PasswordHash))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(definition.Password);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedChargeSettingsAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            if (!await context.TaxSettings.AnyAsync(cancellationToken))
            {
                context.TaxSettings.Add(new TaxSetting
                {
                    Name = "PPN",
                    Percentage = 10m,
                    IsActive = true,
                    CreatedAt = now
                });
            }

            if (!await context.ServiceChargeSettings.AnyAsync(cancellationToken))
            {
                context.ServiceChargeSettings.Add(new ServiceChargeSetting
                {
                    Name = "Service Charge",
                    Percentage = 5m,
                    IsActive = true,
                    CreatedAt = now
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedMembersAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var memberSeeds = new[]
            {
                new MemberSeed("member.regular", "Rani Regular", "081200000001", MemberType.Regular, 15, now.AddMonths(-1)),
                new MemberSeed("member.silver", "Sinta Silver", "081200000002", MemberType.Silver, 90, now.AddMonths(-4)),
                new MemberSeed("member.gold", "Gilang Gold", "081200000003", MemberType.Gold, 210, now.AddMonths(-7)),
                new MemberSeed("member.platinum", "Putra Platinum", "081200000004", MemberType.Platinum, 480, now.AddYears(-1))
            };

            var userLookup = await context.Users
                .Where(user => memberSeeds.Select(seed => seed.Username).Contains(user.Username))
                .ToDictionaryAsync(user => user.Username, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var members = await context.Members
                .Include(member => member.User)
                .ToListAsync(cancellationToken);

            var byUserId = members.ToDictionary(member => member.UserId);
            var hasChanges = false;

            foreach (var seed in memberSeeds)
            {
                if (!userLookup.TryGetValue(seed.Username, out var user))
                {
                    continue;
                }

                if (!byUserId.TryGetValue(user.Id, out var member))
                {
                    context.Members.Add(new Member
                    {
                        UserId = user.Id,
                        FullName = seed.FullName,
                        Phone = seed.Phone,
                        MemberType = seed.MemberType,
                        Points = seed.Points,
                        JoinDate = seed.JoinDate
                    });
                    hasChanges = true;
                    continue;
                }

                if (member.FullName != seed.FullName)
                {
                    member.FullName = seed.FullName;
                    hasChanges = true;
                }

                if (member.Phone != seed.Phone)
                {
                    member.Phone = seed.Phone;
                    hasChanges = true;
                }

                if (member.MemberType != seed.MemberType)
                {
                    member.MemberType = seed.MemberType;
                    hasChanges = true;
                }

                if (member.Points < seed.Points)
                {
                    member.Points = seed.Points;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedTablesAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var definitions = new[]
            {
                new TableSeed("1", 4, TableStatus.Available),
                new TableSeed("2", 4, TableStatus.Available),
                new TableSeed("3", 6, TableStatus.Available),
                new TableSeed("4", 6, TableStatus.Available),
                new TableSeed("5", 8, TableStatus.Available),
                new TableSeed("6", 8, TableStatus.Available),
                new TableSeed("7", 2, TableStatus.Available),
                new TableSeed("8", 2, TableStatus.Available)
            };

            var existing = await context.Tables
                .ToDictionaryAsync(table => table.TableNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var hasChanges = false;
            foreach (var definition in definitions)
            {
                if (existing.TryGetValue(definition.TableNumber, out var table))
                {
                    if (table.Capacity != definition.Capacity)
                    {
                        table.Capacity = definition.Capacity;
                        hasChanges = true;
                    }

                     if (string.IsNullOrWhiteSpace(table.QrCodeUrl))
                    {
                        table.QrCodeUrl = $"/QR/Generate?tableId={table.Id}";
                        hasChanges = true;
                    }

                    continue;
                }

                context.Tables.Add(new Table
                {
                    TableNumber = definition.TableNumber,
                    Capacity = definition.Capacity,
                    Status = definition.Status,
                    CreatedAt = now,
                    QrCodeUrl = string.Empty
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            var tablesNeedingQr = await context.Tables
                .Where(table => string.IsNullOrWhiteSpace(table.QrCodeUrl))
                .ToListAsync(cancellationToken);

            if (tablesNeedingQr.Count > 0)
            {
                foreach (var table in tablesNeedingQr)
                {
                    table.QrCodeUrl = $"/QR/Generate?tableId={table.Id}";
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedProductsAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var categoryLookup = await context.Categories
                .ToDictionaryAsync(category => category.Name, category => category.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var definitions = new[]
            {
                new ProductSeed("Nasi Goreng Spesial", "Nasi goreng dengan ayam, telur, dan sayuran", 35000m, "Makanan", 10m, true),
                new ProductSeed("Mie Ayam Bakso", "Mie ayam dengan bakso spesial", 28000m, "Makanan", 0m, true),
                new ProductSeed("Ayam Goreng Crispy", "Ayam goreng dengan bumbu crispy spesial", 42000m, "Makanan", 15m, true),
                new ProductSeed("Sate Ayam", "Sate ayam dengan bumbu kacang", 30000m, "Makanan", 0m, false),
                new ProductSeed("Es Teh Manis", "Teh manis dengan es", 8000m, "Minuman", 0m, true),
                new ProductSeed("Es Jeruk", "Jeruk segar dengan es", 12000m, "Minuman", 5m, true),
                new ProductSeed("Kopi Susu", "Kopi dengan susu spesial", 18000m, "Minuman", 10m, true),
                new ProductSeed("Jus Alpukat", "Jus alpukat segar", 22000m, "Minuman", 0m, true),
                new ProductSeed("Kentang Goreng", "Kentang goreng crispy", 20000m, "Snack", 5m, true),
                new ProductSeed("Onion Rings", "Bawang bombay goreng tepung", 18000m, "Snack", 0m, true),
                new ProductSeed("Es Krim Coklat", "Es krim coklat premium", 15000m, "Dessert", 10m, true),
                new ProductSeed("Pudding Buah", "Pudding dengan topping buah segar", 22000m, "Dessert", 0m, true)
            };

            var existing = await context.Products
                .ToDictionaryAsync(product => product.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var hasChanges = false;
            foreach (var definition in definitions)
            {
                if (!categoryLookup.TryGetValue(definition.CategoryName, out var categoryId))
                {
                    continue;
                }

                if (existing.TryGetValue(definition.Name, out var product))
                {
                    if (product.Description != definition.Description)
                    {
                        product.Description = definition.Description;
                        hasChanges = true;
                    }

                    if (product.Price != definition.Price)
                    {
                        product.Price = definition.Price;
                        hasChanges = true;
                    }

                    if (product.CategoryId != categoryId)
                    {
                        product.CategoryId = categoryId;
                        hasChanges = true;
                    }

                    if (product.MemberDiscountPercentage != definition.MemberDiscountPercentage)
                    {
                        product.MemberDiscountPercentage = definition.MemberDiscountPercentage;
                        hasChanges = true;
                    }

                    if (product.IsAvailable != definition.IsAvailable)
                    {
                        product.IsAvailable = definition.IsAvailable;
                        hasChanges = true;
                    }

                    continue;
                }

                context.Products.Add(new Product
                {
                    Name = definition.Name,
                    Description = definition.Description,
                    Price = definition.Price,
                    CategoryId = categoryId,
                    MemberDiscountPercentage = definition.MemberDiscountPercentage,
                    IsAvailable = definition.IsAvailable,
                    CreatedAt = now
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedIngredientsAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var definitions = new[]
            {
                new IngredientSeed("Nasi", 50m, 20m, "kg", "Toko Beras Jaya"),
                new IngredientSeed("Ayam", 30m, 15m, "kg", "Supplier Ayam Segar"),
                new IngredientSeed("Minyak Goreng", 10m, 15m, "liter", "Toko Minyak"),
                new IngredientSeed("Telur", 200m, 100m, "butir", "Peternakan Telur"),
                new IngredientSeed("Kopi", 25m, 10m, "kg", "Supplier Kopi")
            };

            var existing = await context.Ingredients
                .ToDictionaryAsync(ingredient => ingredient.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var hasChanges = false;
            foreach (var definition in definitions)
            {
                if (existing.TryGetValue(definition.Name, out var ingredient))
                {
                    if (ingredient.MinStock != definition.MinStock)
                    {
                        ingredient.MinStock = definition.MinStock;
                        hasChanges = true;
                    }

                    if (ingredient.Unit != definition.Unit)
                    {
                        ingredient.Unit = definition.Unit;
                        hasChanges = true;
                    }

                    if (ingredient.Supplier != definition.Supplier)
                    {
                        ingredient.Supplier = definition.Supplier;
                        hasChanges = true;
                    }

                    continue;
                }

                context.Ingredients.Add(new Ingredient
                {
                    Name = definition.Name,
                    StockQuantity = definition.StockQuantity,
                    MinStock = definition.MinStock,
                    Unit = definition.Unit,
                    Supplier = definition.Supplier,
                    CreatedAt = now
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedAssetsAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var definitions = new[]
            {
                new AssetSeed("Meja Makan", AssetType.Meja, 10, "unit", AssetCondition.Baik, now.AddYears(-1), 1200000m),
                new AssetSeed("Kursi", AssetType.Kursi, 38, "unit", AssetCondition.RusakRingan, now.AddYears(-1), 2400000m),
                new AssetSeed("Kompor Gas", AssetType.PeralatanDapur, 3, "unit", AssetCondition.Baik, now.AddMonths(-6), 1800000m),
                new AssetSeed("Kulkas", AssetType.PeralatanElektronik, 2, "unit", AssetCondition.Baik, now.AddYears(-2), 6400000m),
                new AssetSeed("Gelas Kaca", AssetType.PeralatanMinum, 55, "pcs", AssetCondition.Baik, now.AddMonths(-10), 825000m)
            };

            var existing = await context.Assets
                .ToDictionaryAsync(asset => asset.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var hasChanges = false;
            foreach (var definition in definitions)
            {
                if (existing.TryGetValue(definition.Name, out var asset))
                {
                    continue;
                }

                context.Assets.Add(new Asset
                {
                    Name = definition.Name,
                    AssetType = definition.AssetType,
                    Quantity = definition.Quantity,
                    Unit = definition.Unit,
                    Condition = definition.Condition,
                    PurchaseDate = definition.PurchaseDate,
                    PurchasePrice = definition.PurchasePrice,
                    CreatedAt = now
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedDemoTransactionsAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var tableLookup = await context.Tables
                .ToDictionaryAsync(table => table.TableNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var userLookup = await context.Users
                .ToDictionaryAsync(user => user.Username, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var productLookup = await context.Products
                .ToDictionaryAsync(product => product.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var memberLookup = await context.Members
                .Include(member => member.User)
                .ToDictionaryAsync(member => member.User.Username, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var sessionCache = new Dictionary<string, TableSession>(StringComparer.OrdinalIgnoreCase);

            var transactionSeeds = BuildTransactionSeeds(now);
            foreach (var seed in transactionSeeds)
            {
                var exists = await context.Transactions
                    .AnyAsync(transaction => transaction.TransactionNumber == seed.TransactionNumber, cancellationToken);
                if (exists)
                {
                    continue;
                }

                TableSession? tableSession = null;
                if (!string.IsNullOrWhiteSpace(seed.SessionKey) &&
                    !string.IsNullOrWhiteSpace(seed.TableNumber) &&
                    tableLookup.TryGetValue(seed.TableNumber, out var tableForSession))
                {
                    tableSession = await EnsureDemoTableSessionAsync(
                        context,
                        sessionCache,
                        seed,
                        tableForSession,
                        memberLookup,
                        cancellationToken);
                }

                var transaction = new Transaction
                {
                    TransactionNumber = seed.TransactionNumber,
                    TableId = ResolveId(tableLookup, seed.TableNumber),
                    TableSessionId = tableSession?.Id,
                    UserId = ResolveId(userLookup, seed.StaffUsername),
                    CustomerName = seed.CustomerName,
                    CustomerType = seed.CustomerType,
                    Subtotal = seed.Subtotal,
                    Discount = seed.Discount,
                    Tax = seed.Tax,
                    ServiceCharge = seed.ServiceCharge,
                    Total = seed.Total,
                    PaymentMethod = seed.PaymentMethod,
                    PaymentStatus = seed.PaymentStatus,
                    OrderStatus = seed.OrderStatus,
                    PaymentProofUrl = seed.PaymentProofUrl,
                    CreatedAt = seed.CreatedAt,
                    PaidAt = seed.PaidAt
                };

                context.Transactions.Add(transaction);
                await context.SaveChangesAsync(cancellationToken);

                foreach (var detailSeed in seed.Details)
                {
                    if (!productLookup.TryGetValue(detailSeed.ProductName, out var product))
                    {
                        continue;
                    }

                    context.TransactionDetails.Add(new TransactionDetail
                    {
                        TransactionId = transaction.Id,
                        ProductId = product.Id,
                        Quantity = detailSeed.Quantity,
                        UnitPrice = detailSeed.UnitPrice,
                        Notes = detailSeed.Notes,
                        Status = detailSeed.Status,
                        CreatedAt = seed.CreatedAt,
                        CompletedAt = detailSeed.CompletedAt
                    });
                }

                foreach (var notificationSeed in seed.Notifications)
                {
                    context.Notifications.Add(new Notification
                    {
                        TransactionId = transaction.Id,
                        Type = notificationSeed.Type,
                        Message = notificationSeed.Message,
                        Recipient = notificationSeed.Recipient,
                        IsRead = notificationSeed.IsRead,
                        CreatedAt = notificationSeed.CreatedAt
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
            }

            var hasTableChanges = false;
            foreach (var table in tableLookup.Values)
            {
                var hasActiveSession = await context.TableSessions.AnyAsync(
                    session => session.TableId == table.Id &&
                               session.Status == TableSessionStatus.Active &&
                               session.EndTime == null,
                    cancellationToken);

                var expectedStatus = hasActiveSession
                    ? TableStatus.Occupied
                    : table.TableNumber == "3" ? TableStatus.Reserved : TableStatus.Available;

                if (table.Status != expectedStatus)
                {
                    table.Status = expectedStatus;
                    hasTableChanges = true;
                }
            }

            var hasMemberChanges = false;
            if (memberLookup.TryGetValue("member.gold", out var goldMember) && goldMember.Points < 250)
            {
                goldMember.Points = 250;
                hasMemberChanges = true;
            }

            if (memberLookup.TryGetValue("member.platinum", out var platinumMember) && platinumMember.Points < 520)
            {
                platinumMember.Points = 520;
                hasMemberChanges = true;
            }

            if (hasTableChanges || hasMemberChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedDemoAssetLogsAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        {
            var assetLookup = await context.Assets
                .ToDictionaryAsync(asset => asset.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var userLookup = await context.Users
                .ToDictionaryAsync(user => user.Username, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var existingDescriptions = await context.AssetLogs
                .Select(assetLog => assetLog.Description)
                .ToListAsync(cancellationToken);

            var seeds = new[]
            {
                new AssetLogSeed(
                    "Kursi",
                    DamageType.Rusak,
                    2,
                    "supervisor",
                    "[DEMO] Dua kursi tamu patah setelah operasional malam.",
                    now.AddDays(-2),
                    LogStatus.Reported,
                    null,
                    null),
                new AssetLogSeed(
                    "Gelas Kaca",
                    DamageType.Pecah,
                    5,
                    "supervisor",
                    "[DEMO] Gelas pecah setelah event keluarga besar.",
                    now.AddDays(-9),
                    LogStatus.Approved,
                    now.AddDays(-8),
                    "owner")
            };

            var hasChanges = false;
            foreach (var seed in seeds)
            {
                if (existingDescriptions.Contains(seed.Description, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!assetLookup.TryGetValue(seed.AssetName, out var asset) ||
                    !userLookup.TryGetValue(seed.ReportedByUsername, out var reporter))
                {
                    continue;
                }

                context.AssetLogs.Add(new AssetLog
                {
                    AssetId = asset.Id,
                    DamageType = seed.DamageType,
                    Quantity = seed.Quantity,
                    ReportedBy = reporter.Id,
                    Description = seed.Description,
                    ReportedAt = seed.ReportedAt,
                    Status = seed.Status,
                    ApprovedAt = seed.ApprovedAt,
                    ApprovedBy = seed.ApprovedByUsername != null && userLookup.TryGetValue(seed.ApprovedByUsername, out var approver)
                        ? approver.Id
                        : null
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static IReadOnlyList<TransactionSeed> BuildTransactionSeeds(DateTime now)
        {
            var today = now.Date;
            return new[]
            {
                new TransactionSeed(
                    "LUNCH-1",
                    "DEMO-ORD-1001",
                    "1",
                    null,
                    "Budi Tamu",
                    CustomerType.Guest,
                    51000m,
                    0m,
                    5100m,
                    2550m,
                    58650m,
                    PaymentMethod.Transfer,
                    PaymentStatus.Pending,
                    OrderStatus.New,
                    "/uploads/payment-proofs/demo-ord-1001.png",
                    today.AddHours(11),
                    null,
                    new[]
                    {
                        new TransactionDetailSeed("Nasi Goreng Spesial", 1, 35000m, "Tanpa acar", DetailStatus.Pending, null),
                        new TransactionDetailSeed("Es Teh Manis", 2, 8000m, string.Empty, DetailStatus.Pending, null)
                    },
                    new[]
                    {
                        new NotificationSeed(NotificationType.NewOrder, "[DEMO] Pesanan baru dari Meja 1 - DEMO-ORD-1001", "BagianMasak", false, today.AddHours(11))
                    }),
                new TransactionSeed(
                    "LUNCH-2",
                    "DEMO-ORD-1002",
                    "2",
                    null,
                    "Sinta Silver",
                    CustomerType.Member,
                    40000m,
                    2000m,
                    4000m,
                    2000m,
                    44000m,
                    PaymentMethod.QRIS,
                    PaymentStatus.Paid,
                    OrderStatus.Processing,
                    "/uploads/payment-proofs/demo-ord-1002.png",
                    today.AddHours(10),
                    today.AddHours(10).AddMinutes(10),
                    new[]
                    {
                        new TransactionDetailSeed("Kentang Goreng", 1, 20000m, "Extra saus", DetailStatus.Preparing, null),
                        new TransactionDetailSeed("Jus Alpukat", 1, 22000m, "Kurangi gula", DetailStatus.Preparing, null)
                    },
                    new[]
                    {
                        new NotificationSeed(NotificationType.NewOrder, "[DEMO] Pesanan member diproses - DEMO-ORD-1002", "BagianMasak", true, today.AddHours(10))
                    }),
                new TransactionSeed(
                    "TABLE6-LUNCH",
                    "DEMO-POS-2001",
                    "6",
                    "kasir",
                    "Walk-in POS",
                    CustomerType.Guest,
                    60000m,
                    0m,
                    6000m,
                    3000m,
                    69000m,
                    PaymentMethod.Tunai,
                    PaymentStatus.Paid,
                    OrderStatus.Ready,
                    string.Empty,
                    today.AddHours(12).AddMinutes(30),
                    today.AddHours(12).AddMinutes(30),
                    new[]
                    {
                        new TransactionDetailSeed("Ayam Goreng Crispy", 1, 42000m, "Sambal terpisah", DetailStatus.Ready, today.AddHours(12).AddMinutes(55)),
                        new TransactionDetailSeed("Es Jeruk", 1, 12000m, string.Empty, DetailStatus.Ready, today.AddHours(12).AddMinutes(45)),
                        new TransactionDetailSeed("Es Teh Manis", 1, 8000m, string.Empty, DetailStatus.Ready, today.AddHours(12).AddMinutes(40))
                    },
                    new[]
                    {
                        new NotificationSeed(NotificationType.OrderReady, "[DEMO] Pesanan POS siap diambil - DEMO-POS-2001", "Kasir", false, today.AddHours(12).AddMinutes(55))
                    }),
                new TransactionSeed(
                    "TABLE6-LUNCH",
                    "DEMO-ORD-1003",
                    "6",
                    null,
                    "Keluarga Andi",
                    CustomerType.Guest,
                    57000m,
                    0m,
                    5700m,
                    2850m,
                    65550m,
                    PaymentMethod.BayarDiKasir,
                    PaymentStatus.Pending,
                    OrderStatus.Served,
                    string.Empty,
                    today.AddHours(9),
                    null,
                    new[]
                    {
                        new TransactionDetailSeed("Mie Ayam Bakso", 1, 28000m, string.Empty, DetailStatus.Served, today.AddHours(9).AddMinutes(50)),
                        new TransactionDetailSeed("Kopi Susu", 1, 18000m, "Kurang manis", DetailStatus.Served, today.AddHours(9).AddMinutes(45)),
                        new TransactionDetailSeed("Es Krim Coklat", 1, 15000m, string.Empty, DetailStatus.Served, today.AddHours(9).AddMinutes(55))
                    },
                    Array.Empty<NotificationSeed>()),
                new TransactionSeed(
                    "YESTERDAY-4",
                    "DEMO-ORD-0901",
                    "4",
                    "kasir",
                    "Gilang Gold",
                    CustomerType.Member,
                    80000m,
                    8000m,
                    8000m,
                    4000m,
                    84000m,
                    PaymentMethod.Tunai,
                    PaymentStatus.Paid,
                    OrderStatus.Completed,
                    string.Empty,
                    today.AddDays(-1).AddHours(19),
                    today.AddDays(-1).AddHours(19).AddMinutes(5),
                    new[]
                    {
                        new TransactionDetailSeed("Nasi Goreng Spesial", 2, 35000m, string.Empty, DetailStatus.Served, today.AddDays(-1).AddHours(19).AddMinutes(40)),
                        new TransactionDetailSeed("Es Jeruk", 1, 12000m, string.Empty, DetailStatus.Served, today.AddDays(-1).AddHours(19).AddMinutes(35))
                    },
                    Array.Empty<NotificationSeed>()),
                new TransactionSeed(
                    "CORPORATE-5",
                    "DEMO-POS-2002",
                    "5",
                    "kasir",
                    "Corporate Lunch",
                    CustomerType.Guest,
                    156000m,
                    0m,
                    15600m,
                    7800m,
                    179400m,
                    PaymentMethod.Transfer,
                    PaymentStatus.Paid,
                    OrderStatus.Completed,
                    "/uploads/payment-proofs/demo-pos-2002.png",
                    today.AddDays(-4).AddHours(13),
                    today.AddDays(-4).AddHours(13).AddMinutes(20),
                    new[]
                    {
                        new TransactionDetailSeed("Ayam Goreng Crispy", 2, 42000m, string.Empty, DetailStatus.Served, today.AddDays(-4).AddHours(14)),
                        new TransactionDetailSeed("Kopi Susu", 2, 18000m, string.Empty, DetailStatus.Served, today.AddDays(-4).AddHours(13).AddMinutes(50)),
                        new TransactionDetailSeed("Pudding Buah", 1, 22000m, string.Empty, DetailStatus.Served, today.AddDays(-4).AddHours(14).AddMinutes(10))
                    },
                    Array.Empty<NotificationSeed>())
            };
        }

        private static async Task<TableSession> EnsureDemoTableSessionAsync(
            ApplicationDbContext context,
            IDictionary<string, TableSession> sessionCache,
            TransactionSeed seed,
            Table table,
            IReadOnlyDictionary<string, Member> memberLookup,
            CancellationToken cancellationToken)
        {
            if (sessionCache.TryGetValue(seed.SessionKey, out var cachedSession))
            {
                return cachedSession;
            }

            var existingSession = await context.TableSessions
                .FirstOrDefaultAsync(
                    session => session.TableId == table.Id &&
                               session.CustomerName == seed.CustomerName &&
                               session.StartTime == seed.CreatedAt,
                    cancellationToken);

            if (existingSession != null)
            {
                sessionCache[seed.SessionKey] = existingSession;
                return existingSession;
            }

            Member? member = null;
            if (seed.CustomerType == CustomerType.Member)
            {
                memberLookup.TryGetValue(
                    seed.CustomerName switch
                    {
                        "Sinta Silver" => "member.silver",
                        "Gilang Gold" => "member.gold",
                        _ => string.Empty
                    },
                    out member);
            }

            var session = new TableSession
            {
                TableId = table.Id,
                CustomerType = seed.CustomerType,
                MemberId = member?.Id,
                CustomerName = seed.CustomerName,
                StartTime = seed.CreatedAt,
                EndTime = seed.OrderStatus is OrderStatus.Completed or OrderStatus.Cancelled ? seed.PaidAt ?? seed.CreatedAt.AddHours(1) : null,
                Status = seed.OrderStatus is OrderStatus.Completed or OrderStatus.Cancelled
                    ? TableSessionStatus.Closed
                    : TableSessionStatus.Active
            };

            context.TableSessions.Add(session);
            await context.SaveChangesAsync(cancellationToken);

            sessionCache[seed.SessionKey] = session;
            return session;
        }

        private static int? ResolveId<T>(IReadOnlyDictionary<string, T> lookup, string? key)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(key) || !lookup.TryGetValue(key, out var entity))
            {
                return null;
            }

            return entity switch
            {
                Table table => table.Id,
                User user => user.Id,
                _ => null
            };
        }

        private sealed record CategorySeed(string Name, string Description);
        private sealed record UserSeed(string Username, string Email, string Password, UserRole Role, bool IsActive);
        private sealed record MemberSeed(string Username, string FullName, string Phone, MemberType MemberType, int Points, DateTime JoinDate);
        private sealed record TableSeed(string TableNumber, int Capacity, TableStatus Status);
        private sealed record ProductSeed(string Name, string Description, decimal Price, string CategoryName, decimal MemberDiscountPercentage, bool IsAvailable);
        private sealed record IngredientSeed(string Name, decimal StockQuantity, decimal MinStock, string Unit, string Supplier);
        private sealed record AssetSeed(string Name, AssetType AssetType, int Quantity, string Unit, AssetCondition Condition, DateTime PurchaseDate, decimal PurchasePrice);
        private sealed record AssetLogSeed(
            string AssetName,
            DamageType DamageType,
            int Quantity,
            string ReportedByUsername,
            string Description,
            DateTime ReportedAt,
            LogStatus Status,
            DateTime? ApprovedAt,
            string? ApprovedByUsername);
        private sealed record TransactionSeed(
            string SessionKey,
            string TransactionNumber,
            string? TableNumber,
            string? StaffUsername,
            string CustomerName,
            CustomerType CustomerType,
            decimal Subtotal,
            decimal Discount,
            decimal Tax,
            decimal ServiceCharge,
            decimal Total,
            PaymentMethod PaymentMethod,
            PaymentStatus PaymentStatus,
            OrderStatus OrderStatus,
            string PaymentProofUrl,
            DateTime CreatedAt,
            DateTime? PaidAt,
            IReadOnlyList<TransactionDetailSeed> Details,
            IReadOnlyList<NotificationSeed> Notifications);
        private sealed record TransactionDetailSeed(
            string ProductName,
            int Quantity,
            decimal UnitPrice,
            string Notes,
            DetailStatus Status,
            DateTime? CompletedAt);
        private sealed record NotificationSeed(
            NotificationType Type,
            string Message,
            string Recipient,
            bool IsRead,
            DateTime CreatedAt);
    }
}
