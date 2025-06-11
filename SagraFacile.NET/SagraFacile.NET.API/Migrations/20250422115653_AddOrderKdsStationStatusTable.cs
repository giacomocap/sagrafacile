using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagraFacile.NET.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderKdsStationStatusTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderKdsStationStatuses",
                columns: table => new
                {
                    OrderId = table.Column<string>(type: "character varying(50)", nullable: false),
                    KdsStationId = table.Column<int>(type: "integer", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderKdsStationStatuses", x => new { x.OrderId, x.KdsStationId });
                    table.ForeignKey(
                        name: "FK_OrderKdsStationStatuses_KdsStations_KdsStationId",
                        column: x => x.KdsStationId,
                        principalTable: "KdsStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderKdsStationStatuses_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderKdsStationStatuses_KdsStationId",
                table: "OrderKdsStationStatuses",
                column: "KdsStationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderKdsStationStatuses");
        }
    }
}
