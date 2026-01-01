using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAiVideoJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiVideoMode",
                table: "ContentDrafts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiVideoPromptOverride",
                table: "ContentDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GenerateAiVideo",
                table: "ContentDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiVideoJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "OpenAI"),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Seconds = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OpenAiVideoId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVideoJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiVideoJobs_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiVideoJobs_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiVideoJobs_ContentItemId",
                table: "AiVideoJobs",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AiVideoJobs_MediaAssetId",
                table: "AiVideoJobs",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AiVideoJobs_Status",
                table: "AiVideoJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AiVideoJobs_Status_CreatedAtUtc",
                table: "AiVideoJobs",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiVideoJobs");

            migrationBuilder.DropColumn(
                name: "AiVideoMode",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "AiVideoPromptOverride",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "GenerateAiVideo",
                table: "ContentDrafts");
        }
    }
}
