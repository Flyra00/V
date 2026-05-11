using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restoran.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndUserRoleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsSystemRole = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "Code", "IsSystemRole", "IsActive", "SortOrder", "CreatedAt" },
                values: new object[,]
                {
                    { 1, "Administrator", "Admin", true, true, 1, new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Owner", "Owner", true, true, 2, new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Supervisor", "Supervisor", true, true, 3, new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Kasir", "Kasir", true, true, 4, new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "Bagian Masak", "BagianMasak", true, true, 5, new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "Member", "Member", true, true, 6, new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "RoleId" = CASE "Role"
                    WHEN 0 THEN 1
                    WHEN 4 THEN 2
                    WHEN 1 THEN 3
                    WHEN 2 THEN 4
                    WHEN 3 THEN 5
                    WHEN 5 THEN 6
                    ELSE NULL
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "RoleId" = NULL
                """);

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");
        }
    }
}
