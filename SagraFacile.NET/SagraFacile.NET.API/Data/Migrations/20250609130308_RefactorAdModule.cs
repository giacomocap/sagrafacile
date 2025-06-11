using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAdModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdMediaItems_Areas_AreaId",
                table: "AdMediaItems");

            migrationBuilder.DropIndex(
                name: "IX_AdMediaItems_AreaId",
                table: "AdMediaItems");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "AdMediaItems");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "AdMediaItems");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AdMediaItems");

            migrationBuilder.RenameColumn(
                name: "DisplayOrder",
                table: "AdMediaItems",
                newName: "OrganizationId");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AdMediaItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AdAreaAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdMediaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdAreaAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdAreaAssignments_AdMediaItems_AdMediaItemId",
                        column: x => x.AdMediaItemId,
                        principalTable: "AdMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdAreaAssignments_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdMediaItems_OrganizationId",
                table: "AdMediaItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdAreaAssignments_AdMediaItemId_AreaId",
                table: "AdAreaAssignments",
                columns: new[] { "AdMediaItemId", "AreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdAreaAssignments_AreaId",
                table: "AdAreaAssignments",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdMediaItems_Organizations_OrganizationId",
                table: "AdMediaItems",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdMediaItems_Organizations_OrganizationId",
                table: "AdMediaItems");

            migrationBuilder.DropTable(
                name: "AdAreaAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AdMediaItems_OrganizationId",
                table: "AdMediaItems");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AdMediaItems");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "AdMediaItems",
                newName: "DisplayOrder");

            migrationBuilder.AddColumn<int>(
                name: "AreaId",
                table: "AdMediaItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "AdMediaItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AdMediaItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AdMediaItems_AreaId",
                table: "AdMediaItems",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdMediaItems_Areas_AreaId",
                table: "AdMediaItems",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
