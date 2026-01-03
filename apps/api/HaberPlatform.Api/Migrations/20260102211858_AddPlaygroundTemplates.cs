using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaygroundTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "FullTextFetchEnabled",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "FullTextExtractMode",
                table: "Sources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Auto",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "IsTruncated",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleFetchError",
                table: "ContentItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PublishTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RuleJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublishTemplateSpecs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisualSpecJson = table.Column<string>(type: "text", nullable: true),
                    TextSpecJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishTemplateSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublishTemplateSpecs_PublishTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PublishTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublishTemplates_Format",
                table: "PublishTemplates",
                column: "Format");

            migrationBuilder.CreateIndex(
                name: "IX_PublishTemplates_IsActive_Priority",
                table: "PublishTemplates",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_PublishTemplates_Name",
                table: "PublishTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublishTemplates_Platform",
                table: "PublishTemplates",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_PublishTemplates_Platform_Format_IsActive",
                table: "PublishTemplates",
                columns: new[] { "Platform", "Format", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PublishTemplateSpecs_TemplateId",
                table: "PublishTemplateSpecs",
                column: "TemplateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateAssets_Key",
                table: "TemplateAssets",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublishTemplateSpecs");

            migrationBuilder.DropTable(
                name: "TemplateAssets");

            migrationBuilder.DropTable(
                name: "PublishTemplates");

            migrationBuilder.AlterColumn<bool>(
                name: "FullTextFetchEnabled",
                table: "Sources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "FullTextExtractMode",
                table: "Sources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Auto");

            migrationBuilder.AlterColumn<bool>(
                name: "IsTruncated",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "ArticleFetchError",
                table: "ContentItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
