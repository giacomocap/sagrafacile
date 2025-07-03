using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SagraFacile.NET.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertOrganizationIdToGuidAndAddSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration changes the primary key of the Organizations table from int to Guid.
            // It also changes all foreign keys that reference it.
            // This is a complex operation that requires manual SQL to ensure data integrity.
            // The process is as follows:
            // 1. Add the new SubscriptionStatus column to Organizations.
            // 2. Create a temporary function to generate deterministic GUIDs from integers.
            // 3. Drop all foreign key constraints that reference the old integer-based OrganizationId.
            // 4. Alter the column types from int to Guid using the temporary function.
            // 5. Re-create all the foreign key constraints with the new Guid columns.
            // 6. Drop the temporary function.

            // Step 1: Add the new SubscriptionStatus column to Organizations, with a default value.
            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Organizations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Free");

            // Step 2: Create a temporary function to generate deterministic GUIDs from integers.
            var namespaceUuid = "f4a8c3a0-9b1d-4e7a-8f0c-1d2b3c4d5e6f"; // A randomly generated, fixed namespace
            migrationBuilder.Sql($@"
                CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";
                CREATE OR REPLACE FUNCTION temp_int_to_guid(id integer) RETURNS uuid AS $$
                BEGIN
                    -- Using uuid_generate_v5 to create a deterministic UUID from the integer ID.
                    RETURN uuid_generate_v5('{namespaceUuid}'::uuid, id::text);
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Step 3: Drop all foreign key constraints that reference the old integer-based OrganizationId.
            // We assume the conventional names EF Core creates. If these fail, they need to be adjusted to match the database.
            migrationBuilder.DropForeignKey(name: "FK_AdMediaItems_Organizations_OrganizationId", table: "AdMediaItems");
            migrationBuilder.DropForeignKey(name: "FK_Areas_Organizations_OrganizationId", table: "Areas");
            migrationBuilder.DropForeignKey(name: "FK_AspNetUsers_Organizations_OrganizationId", table: "AspNetUsers");
            migrationBuilder.DropForeignKey(name: "FK_CashierStations_Organizations_OrganizationId", table: "CashierStations");
            migrationBuilder.DropForeignKey(name: "FK_Days_Organizations_OrganizationId", table: "Days");
            migrationBuilder.DropForeignKey(name: "FK_KdsStations_Organizations_OrganizationId", table: "KdsStations");
            migrationBuilder.DropForeignKey(name: "FK_Orders_Organizations_OrganizationId", table: "Orders");
            migrationBuilder.DropForeignKey(name: "FK_Printers_Organizations_OrganizationId", table: "Printers");
            migrationBuilder.DropForeignKey(name: "FK_PrintJobs_Organizations_OrganizationId", table: "PrintJobs");
            migrationBuilder.DropForeignKey(name: "FK_PrintTemplates_Organizations_OrganizationId", table: "PrintTemplates");
            migrationBuilder.DropForeignKey(name: "FK_SyncConfigurations_Organizations_OrganizationId", table: "SyncConfigurations");

            // Step 4: Alter the column types from int to Guid using the temporary function.
            // First, alter all the foreign key columns.
            var tablesWithOrgFk = new[] {
                "SyncConfigurations", "PrintTemplates", "PrintJobs", "Printers",
                "Orders", "KdsStations", "Days", "CashierStations",
                "AspNetUsers", "Areas", "AdMediaItems"
            };

            foreach (var table in tablesWithOrgFk)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ALTER COLUMN ""OrganizationId"" TYPE uuid USING (temp_int_to_guid(""OrganizationId""));");
            }

            // Now, alter the primary key column in the Organizations table.
            // First, we must drop the IDENTITY property before changing the type.
            migrationBuilder.Sql(@"ALTER TABLE ""Organizations"" ALTER COLUMN ""Id"" DROP IDENTITY;");
            migrationBuilder.Sql(@"ALTER TABLE ""Organizations"" ALTER COLUMN ""Id"" TYPE uuid USING (temp_int_to_guid(""Id""));");
            
            // Also, we need to make the OrganizationId in AspNetUsers nullable as it was before.
            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: false);


            // Step 5: Re-create all the foreign key constraints with the new Guid columns.
            migrationBuilder.AddForeignKey(
                name: "FK_AdMediaItems_Organizations_OrganizationId",
                table: "AdMediaItems",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Organizations_OrganizationId",
                table: "Areas",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_CashierStations_Organizations_OrganizationId",
                table: "CashierStations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Days_Organizations_OrganizationId",
                table: "Days",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_KdsStations_Organizations_OrganizationId",
                table: "KdsStations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Organizations_OrganizationId",
                table: "Orders",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Printers_Organizations_OrganizationId",
                table: "Printers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Organizations_OrganizationId",
                table: "PrintJobs",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_PrintTemplates_Organizations_OrganizationId",
                table: "PrintTemplates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_SyncConfigurations_Organizations_OrganizationId",
                table: "SyncConfigurations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Step 6: Drop the temporary function.
            migrationBuilder.Sql("DROP FUNCTION temp_int_to_guid(integer);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Organizations");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "SyncConfigurations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "PrintTemplates",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "PrintJobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Printers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Organizations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Orders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "KdsStations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Days",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "CashierStations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Areas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "AdMediaItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
