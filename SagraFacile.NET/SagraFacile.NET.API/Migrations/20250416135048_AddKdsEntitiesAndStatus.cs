using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SagraFacile.NET.API.Migrations
{
    /// <inheritdoc />
    public partial class AddKdsEntitiesAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KdsStatus",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "KdsStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AreaId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KdsStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KdsStations_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KdsStations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KdsCategoryAssignments",
                columns: table => new
                {
                    KdsStationId = table.Column<int>(type: "integer", nullable: false),
                    MenuCategoryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KdsCategoryAssignments", x => new { x.KdsStationId, x.MenuCategoryId });
                    table.ForeignKey(
                        name: "FK_KdsCategoryAssignments_KdsStations_KdsStationId",
                        column: x => x.KdsStationId,
                        principalTable: "KdsStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KdsCategoryAssignments_MenuCategories_MenuCategoryId",
                        column: x => x.MenuCategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KdsCategoryAssignments_MenuCategoryId",
                table: "KdsCategoryAssignments",
                column: "MenuCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KdsStations_AreaId",
                table: "KdsStations",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_KdsStations_OrganizationId",
                table: "KdsStations",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KdsCategoryAssignments");

            migrationBuilder.DropTable(
                name: "KdsStations");

            migrationBuilder.DropColumn(
                name: "KdsStatus",
                table: "OrderItems");
        }
    }
}
