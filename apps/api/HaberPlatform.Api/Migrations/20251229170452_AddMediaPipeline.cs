using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimaryImageUrl",
                table: "PublishedContents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoGenerateImageIfMissing",
                table: "ContentDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePromptOverride",
                table: "ContentDrafts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageStylePreset",
                table: "ContentDrafts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GenerationPrompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentMediaLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentMediaLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentMediaLinks_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentMediaLinks_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentMediaLinks_ContentItemId_IsPrimary",
                table: "ContentMediaLinks",
                columns: new[] { "ContentItemId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentMediaLinks_ContentItemId_MediaAssetId",
                table: "ContentMediaLinks",
                columns: new[] { "ContentItemId", "MediaAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentMediaLinks_ContentItemId_SortOrder",
                table: "ContentMediaLinks",
                columns: new[] { "ContentItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentMediaLinks_MediaAssetId",
                table: "ContentMediaLinks",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_CreatedAtUtc",
                table: "MediaAssets",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Origin",
                table: "MediaAssets",
                column: "Origin");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Sha256",
                table: "MediaAssets",
                column: "Sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentMediaLinks");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "PrimaryImageUrl",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "AutoGenerateImageIfMissing",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "ImagePromptOverride",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "ImageStylePreset",
                table: "ContentDrafts");
        }
    }
}
