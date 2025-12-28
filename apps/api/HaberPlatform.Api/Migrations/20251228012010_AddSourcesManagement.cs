using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcesManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TrustLevel",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 50,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Sources",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Sources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Gundem");

            migrationBuilder.AddColumn<string>(
                name: "DefaultBehavior",
                table: "Sources",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Editorial");

            migrationBuilder.AddColumn<string>(
                name: "Identifier",
                table: "Sources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            // Set UpdatedAtUtc = CreatedAtUtc for existing rows
            migrationBuilder.Sql("UPDATE \"Sources\" SET \"UpdatedAtUtc\" = \"CreatedAtUtc\"");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Category",
                table: "Sources",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_IsActive_Priority",
                table: "Sources",
                columns: new[] { "IsActive", "Priority" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Type",
                table: "Sources",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Type_Identifier",
                table: "Sources",
                columns: new[] { "Type", "Identifier" },
                unique: true,
                filter: "\"Identifier\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Url",
                table: "Sources",
                column: "Url",
                unique: true,
                filter: "\"Url\" IS NOT NULL AND \"Type\" = 'RSS'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sources_Category",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_IsActive_Priority",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_Type",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_Type_Identifier",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_Url",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DefaultBehavior",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "Identifier",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Sources");

            migrationBuilder.AlterColumn<int>(
                name: "TrustLevel",
                table: "Sources",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 50);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "Sources",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Sources",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Sources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
