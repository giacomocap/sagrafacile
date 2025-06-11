using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterEntitiesAndConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrintComandasAtCashier",
                table: "Areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptPrinterId",
                table: "Areas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ConnectionString = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    WindowsPrinterName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Printers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrinterCategoryAssignments",
                columns: table => new
                {
                    PrinterId = table.Column<int>(type: "integer", nullable: false),
                    MenuCategoryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterCategoryAssignments", x => new { x.PrinterId, x.MenuCategoryId });
                    table.ForeignKey(
                        name: "FK_PrinterCategoryAssignments_MenuCategories_MenuCategoryId",
                        column: x => x.MenuCategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrinterCategoryAssignments_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_ReceiptPrinterId",
                table: "Areas",
                column: "ReceiptPrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterCategoryAssignments_MenuCategoryId",
                table: "PrinterCategoryAssignments",
                column: "MenuCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_OrganizationId",
                table: "Printers",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Printers_ReceiptPrinterId",
                table: "Areas",
                column: "ReceiptPrinterId",
                principalTable: "Printers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Printers_ReceiptPrinterId",
                table: "Areas");

            migrationBuilder.DropTable(
                name: "PrinterCategoryAssignments");

            migrationBuilder.DropTable(
                name: "Printers");

            migrationBuilder.DropIndex(
                name: "IX_Areas_ReceiptPrinterId",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "PrintComandasAtCashier",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "ReceiptPrinterId",
                table: "Areas");
        }
    }
}
