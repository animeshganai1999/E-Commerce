using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceBackend.Infrastructure.Migrations
{
    public partial class EnforceUniqueCartProducts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH RankedCartItems AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY UserId, ProductId ORDER BY Id) AS RowNumber,
                        SUM(Quantity) OVER (PARTITION BY UserId, ProductId) AS TotalQuantity
                    FROM CartItems
                )
                UPDATE cart
                SET Quantity =
                    CASE
                        WHEN ranked.TotalQuantity > 100 THEN 100
                        ELSE ranked.TotalQuantity
                    END
                FROM CartItems AS cart
                INNER JOIN RankedCartItems AS ranked ON ranked.Id = cart.Id
                WHERE ranked.RowNumber = 1;

                WITH RankedCartItems AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY UserId, ProductId ORDER BY Id) AS RowNumber
                    FROM CartItems
                )
                DELETE cart
                FROM CartItems AS cart
                INNER JOIN RankedCartItems AS ranked ON ranked.Id = cart.Id
                WHERE ranked.RowNumber > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CartItems_UserId",
                table: "CartItems");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_UserId_ProductId",
                table: "CartItems",
                columns: new[] { "UserId", "ProductId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_UserId_ProductId",
                table: "CartItems");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_UserId",
                table: "CartItems",
                column: "UserId");
        }
    }
}
