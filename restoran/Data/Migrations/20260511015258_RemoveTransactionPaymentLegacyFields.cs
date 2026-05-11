using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restoran.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTransactionPaymentLegacyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "Payments" ("TransactionId", "PaymentMethodOptionId", "Amount", "PaymentStatus", "PaymentDate", "ProofUrl", "CreatedAt", "UpdatedAt")
                SELECT
                    t."Id",
                    method."Id",
                    t."Total",
                    t."PaymentStatus",
                    t."PaidAt",
                    COALESCE(t."PaymentProofUrl", ''),
                    t."CreatedAt",
                    COALESCE(t."PaidAt", t."CreatedAt")
                FROM "Transactions" AS t
                INNER JOIN "PaymentMethodOptions" AS method
                    ON method."LegacyMethod" = t."PaymentMethod"
                LEFT JOIN "Payments" AS payment
                    ON payment."TransactionId" = t."Id"
                WHERE payment."Id" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Payments"
                SET
                    "PaymentMethodOptionId" = (
                        SELECT method."Id"
                        FROM "Transactions" AS transactionData
                        INNER JOIN "PaymentMethodOptions" AS method
                            ON method."LegacyMethod" = transactionData."PaymentMethod"
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ),
                    "Amount" = (
                        SELECT transactionData."Total"
                        FROM "Transactions" AS transactionData
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ),
                    "PaymentStatus" = (
                        SELECT transactionData."PaymentStatus"
                        FROM "Transactions" AS transactionData
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ),
                    "PaymentDate" = (
                        SELECT transactionData."PaidAt"
                        FROM "Transactions" AS transactionData
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ),
                    "ProofUrl" = COALESCE((
                        SELECT transactionData."PaymentProofUrl"
                        FROM "Transactions" AS transactionData
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ), ''),
                    "UpdatedAt" = COALESCE((
                        SELECT transactionData."PaidAt"
                        FROM "Transactions" AS transactionData
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ), (
                        SELECT transactionData."CreatedAt"
                        FROM "Transactions" AS transactionData
                        WHERE transactionData."Id" = "Payments"."TransactionId"
                    ), "UpdatedAt")
                WHERE EXISTS (
                    SELECT 1
                    FROM "Transactions" AS transactionData
                    WHERE transactionData."Id" = "Payments"."TransactionId"
                );
                """);

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentProofUrl",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProofUrl",
                table: "Transactions",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "Transactions"
                SET
                    "PaymentMethod" = COALESCE((
                        SELECT method."LegacyMethod"
                        FROM "Payments" AS payment
                        INNER JOIN "PaymentMethodOptions" AS method
                            ON method."Id" = payment."PaymentMethodOptionId"
                        WHERE payment."TransactionId" = "Transactions"."Id"
                    ), "PaymentMethod"),
                    "PaymentStatus" = COALESCE((
                        SELECT payment."PaymentStatus"
                        FROM "Payments" AS payment
                        WHERE payment."TransactionId" = "Transactions"."Id"
                    ), "PaymentStatus"),
                    "PaidAt" = (
                        SELECT payment."PaymentDate"
                        FROM "Payments" AS payment
                        WHERE payment."TransactionId" = "Transactions"."Id"
                    ),
                    "PaymentProofUrl" = COALESCE((
                        SELECT payment."ProofUrl"
                        FROM "Payments" AS payment
                        WHERE payment."TransactionId" = "Transactions"."Id"
                    ), "PaymentProofUrl")
                WHERE EXISTS (
                    SELECT 1
                    FROM "Payments" AS payment
                    WHERE payment."TransactionId" = "Transactions"."Id"
                );
                """);
        }
    }
}
