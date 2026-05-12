using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restoran.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionTrackingToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "Transactions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Transactions""
                SET ""TrackingToken"" = lower(hex(randomblob(16)))
                WHERE ""TrackingToken"" IS NULL OR trim(""TrackingToken"") = '';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TrackingToken",
                table: "Transactions",
                column: "TrackingToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TrackingToken",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                table: "Transactions");
        }
    }
}
