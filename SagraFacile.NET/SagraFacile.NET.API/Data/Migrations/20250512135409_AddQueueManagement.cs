using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableQueueSystem",
                table: "Areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AreaQueueStates",
                columns: table => new
                {
                    AreaQueueStateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AreaId = table.Column<int>(type: "integer", nullable: false),
                    NextSequentialNumber = table.Column<int>(type: "integer", nullable: false),
                    LastCalledNumber = table.Column<int>(type: "integer", nullable: true),
                    LastCalledCashierStationId = table.Column<int>(type: "integer", nullable: true),
                    LastCallTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastResetTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaQueueStates", x => x.AreaQueueStateId);
                    table.ForeignKey(
                        name: "FK_AreaQueueStates_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AreaQueueStates_CashierStations_LastCalledCashierStationId",
                        column: x => x.LastCalledCashierStationId,
                        principalTable: "CashierStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaQueueStates_AreaId",
                table: "AreaQueueStates",
                column: "AreaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AreaQueueStates_LastCalledCashierStationId",
                table: "AreaQueueStates",
                column: "LastCalledCashierStationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreaQueueStates");

            migrationBuilder.DropColumn(
                name: "EnableQueueSystem",
                table: "Areas");
        }
    }
}
