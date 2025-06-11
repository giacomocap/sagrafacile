using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierStationAndLinkToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CashierStationId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashierStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceiptPrinterId = table.Column<int>(type: "integer", nullable: false),
                    PrintComandasAtThisStation = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierStations_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierStations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierStations_Printers_ReceiptPrinterId",
                        column: x => x.ReceiptPrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CashierStationId",
                table: "Orders",
                column: "CashierStationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierStations_AreaId",
                table: "CashierStations",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierStations_OrganizationId",
                table: "CashierStations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierStations_ReceiptPrinterId",
                table: "CashierStations",
                column: "ReceiptPrinterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_CashierStations_CashierStationId",
                table: "Orders",
                column: "CashierStationId",
                principalTable: "CashierStations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_CashierStations_CashierStationId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "CashierStations");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CashierStationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CashierStationId",
                table: "Orders");
        }
    }
}
