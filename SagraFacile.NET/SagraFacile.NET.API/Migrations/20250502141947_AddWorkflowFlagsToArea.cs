using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagraFacile.NET.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowFlagsToArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableCompletionConfirmation",
                table: "Areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableKds",
                table: "Areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableWaiterConfirmation",
                table: "Areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableCompletionConfirmation",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "EnableKds",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "EnableWaiterConfirmation",
                table: "Areas");
        }
    }
}
