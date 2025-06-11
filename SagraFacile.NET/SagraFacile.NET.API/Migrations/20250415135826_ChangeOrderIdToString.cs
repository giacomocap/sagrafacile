using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SagraFacile.NET.API.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOrderIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_AreaId_OrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrganizationId_OrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "Orders");

            // --- Drop/Recreate Approach ---
            // 1. Drop FK constraint referencing Orders.Id
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Orders_OrderId", // Use the actual FK name if different
                table: "OrderItems");

            // 2. Drop PK constraint on Orders.Id
            migrationBuilder.DropPrimaryKey(
                name: "PK_Orders", // Use the actual PK name if different
                table: "Orders");

            // 3. Drop OrderItems.OrderId column
            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "OrderItems");

            // 4. Drop Orders.Id column
            migrationBuilder.DropColumn(
                name: "Id",
                table: "Orders");

            // 5. Add Orders.Id column back as string
            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Orders",
                type: "character varying(100)", // Match model definition
                maxLength: 100,
                nullable: false);

            // 6. Add PK constraint back to Orders.Id
            migrationBuilder.AddPrimaryKey(
                name: "PK_Orders",
                table: "Orders",
                column: "Id");

            // 7. Add OrderItems.OrderId column back as string
            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                table: "OrderItems",
                type: "character varying(100)", // Match model definition
                maxLength: 100,
                nullable: false);

            // 8. Add FK constraint back
            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Orders_OrderId",
                table: "OrderItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade); // Assuming Cascade was the original behavior, adjust if needed

            // --- End Drop/Recreate Approach ---

            // Recreate original indexes (excluding the ones involving OrderNumber)
            migrationBuilder.CreateIndex(
                name: "IX_Orders_AreaId",
                table: "Orders",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId",
                table: "Orders",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_AreaId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrganizationId",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Orders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AreaId_OrderNumber",
                table: "Orders",
                columns: new[] { "AreaId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_OrderNumber",
                table: "Orders",
                columns: new[] { "OrganizationId", "OrderNumber" });
        }
    }
}
