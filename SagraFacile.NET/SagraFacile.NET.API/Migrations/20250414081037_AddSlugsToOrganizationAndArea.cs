using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagraFacile.NET.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugsToOrganizationAndArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Areas_OrganizationId",
                table: "Areas");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Areas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // SQL to update existing Organization slugs based on Name
            migrationBuilder.Sql(@"
                UPDATE ""Organizations""
                SET ""Slug"" = trim(both '-' from regexp_replace(lower(""Name""), '[^a-z0-9]+', '-', 'g'))
                WHERE ""Slug"" = '';
            ");

            // SQL to update existing Area slugs based on Name
            migrationBuilder.Sql(@"
                UPDATE ""Areas""
                SET ""Slug"" = trim(both '-' from regexp_replace(lower(""Name""), '[^a-z0-9]+', '-', 'g'))
                WHERE ""Slug"" = '';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Areas_OrganizationId_Slug",
                table: "Areas",
                columns: new[] { "OrganizationId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Areas_OrganizationId_Slug",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Areas");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_OrganizationId",
                table: "Areas",
                column: "OrganizationId");
        }
    }
}
