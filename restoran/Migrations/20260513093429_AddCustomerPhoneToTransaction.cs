using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restoran.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPhoneToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "Transactions");
        }
    }
}
