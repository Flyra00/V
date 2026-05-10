using Microsoft.EntityFrameworkCore;
using Restoran.Models;

namespace Restoran.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductIngredient> ProductIngredients { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetLog> AssetLogs { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<TableSession> TableSessions { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionDetail> TransactionDetails { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<TaxSetting> TaxSettings { get; set; }
        public DbSet<ServiceChargeSetting> ServiceChargeSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Table>()
                .HasIndex(t => t.TableNumber)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductIngredient>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.ProductIngredients)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductIngredient>()
                .HasOne(pi => pi.Ingredient)
                .WithMany(i => i.ProductIngredients)
                .HasForeignKey(pi => pi.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.TransactionNumber)
                .IsUnique();

            modelBuilder.Entity<TableSession>()
                .HasIndex(session => new { session.TableId, session.Status });

            modelBuilder.Entity<TaxSetting>()
                .HasIndex(t => t.Name)
                .IsUnique();

            modelBuilder.Entity<ServiceChargeSetting>()
                .HasIndex(s => s.Name)
                .IsUnique();

            // Configure AssetLog - User relationships
            modelBuilder.Entity<AssetLog>()
                .HasOne(al => al.Reporter)
                .WithMany(u => u.AssetLogs)
                .HasForeignKey(al => al.ReportedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AssetLog>()
                .HasOne(al => al.Approver)
                .WithMany()
                .HasForeignKey(al => al.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TableSession>()
                .HasOne(session => session.Table)
                .WithMany(table => table.TableSessions)
                .HasForeignKey(session => session.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TableSession>()
                .HasOne(session => session.Member)
                .WithMany()
                .HasForeignKey(session => session.MemberId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Transaction>()
                .HasOne(transaction => transaction.TableSession)
                .WithMany(session => session.Transactions)
                .HasForeignKey(transaction => transaction.TableSessionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Note: Data seeding is handled by SeedData.Initialize() in Program.cs
            // untuk In-Memory Database testing
        }
    }
}
