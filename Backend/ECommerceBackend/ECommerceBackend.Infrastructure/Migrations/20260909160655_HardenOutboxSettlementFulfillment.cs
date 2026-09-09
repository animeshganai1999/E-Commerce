using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenOutboxSettlementFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "UserInvoice",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlobUploadedAt",
                table: "UserInvoice",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailDispatchedAt",
                table: "UserInvoice",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfilledAt",
                table: "UserInvoice",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "UserInvoice",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "UserInvoice",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "UserInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE OutboxMessages
                SET Error = LEFT(Error, 2000)
                WHERE LEN(Error) > 2000;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "OutboxMessages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AggregateId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserInvoice_OrderId",
                table: "UserInvoice",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_FailedAt_NextAttemptAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "FailedAt", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Type_AggregateId",
                table: "OutboxMessages",
                columns: new[] { "Type", "AggregateId" },
                unique: true,
                filter: "[AggregateId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_UserInvoice_Orders_OrderId",
                table: "UserInvoice",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserInvoice_Orders_OrderId",
                table: "UserInvoice");

            migrationBuilder.DropIndex(
                name: "IX_UserInvoice_OrderId",
                table: "UserInvoice");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_FailedAt_NextAttemptAt_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Type_AggregateId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "BlobUploadedAt",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "EmailDispatchedAt",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "FulfilledAt",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "UserInvoice");

            migrationBuilder.DropColumn(
                name: "AggregateId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt",
                table: "OutboxMessages",
                column: "ProcessedAt");
        }
    }
}
