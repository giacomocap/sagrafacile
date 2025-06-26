using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperSizeToPrinter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaperSize",
                table: "Printers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaperSize",
                table: "Printers");
        }
    }
}
