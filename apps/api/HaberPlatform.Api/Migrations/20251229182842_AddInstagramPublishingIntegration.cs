using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInstagramPublishingIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstagramCaptionOverride",
                table: "ContentDrafts",
                type: "character varying(2200)",
                maxLength: 2200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublishToInstagram",
                table: "ContentDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "InstagramConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FacebookUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PageId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PageName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IgUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IgUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScopesCsv = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PageAccessTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDefaultPublisher = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstagramConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstagramConnections_IgUserId",
                table: "InstagramConnections",
                column: "IgUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InstagramConnections_IsDefaultPublisher",
                table: "InstagramConnections",
                column: "IsDefaultPublisher");

            migrationBuilder.CreateIndex(
                name: "IX_InstagramConnections_PageId",
                table: "InstagramConnections",
                column: "PageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstagramConnections");

            migrationBuilder.DropColumn(
                name: "InstagramCaptionOverride",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "PublishToInstagram",
                table: "ContentDrafts");
        }
    }
}
