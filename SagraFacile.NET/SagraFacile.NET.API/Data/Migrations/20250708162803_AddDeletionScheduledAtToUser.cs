using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletionScheduledAtToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionScheduledAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionScheduledAt",
                table: "AspNetUsers");
        }
    }
}
