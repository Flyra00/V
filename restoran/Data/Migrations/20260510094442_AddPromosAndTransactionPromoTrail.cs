using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restoran.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromosAndTransactionPromoTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Promos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PromoType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MinimumPurchase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promos", x => x.Id);
                });

            migrationBuilder.AddColumn<string>(
                name: "AppliedPromoName",
                table: "Transactions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<int>(
                name: "PromoId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promos_Name",
                table: "Promos",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PromoId",
                table: "Transactions",
                column: "PromoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Promos_PromoId",
                table: "Transactions",
                column: "PromoId",
                principalTable: "Promos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Promos_PromoId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Promos");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_PromoId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AppliedPromoName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PromoId",
                table: "Transactions");
        }
    }
}
