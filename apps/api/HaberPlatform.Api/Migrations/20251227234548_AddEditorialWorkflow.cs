using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditorialNote",
                table: "ContentItems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEditedAtUtc",
                table: "ContentItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditedByUserId",
                table: "ContentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ContentItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    XText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WebTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WebBody = table.Column<string>(type: "text", nullable: true),
                    MobileSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PushTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PushBody = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HashtagsCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MentionsCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentDrafts_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentDrafts_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ContentRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentRevisions_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_LastEditedByUserId",
                table: "ContentItems",
                column: "LastEditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDrafts_ContentItemId",
                table: "ContentDrafts",
                column: "ContentItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentDrafts_UpdatedByUserId",
                table: "ContentDrafts",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentRevisions_ContentItemId_VersionNo",
                table: "ContentRevisions",
                columns: new[] { "ContentItemId", "VersionNo" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ContentRevisions_CreatedByUserId",
                table: "ContentRevisions",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_Users_LastEditedByUserId",
                table: "ContentItems",
                column: "LastEditedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_Users_LastEditedByUserId",
                table: "ContentItems");

            migrationBuilder.DropTable(
                name: "ContentDrafts");

            migrationBuilder.DropTable(
                name: "ContentRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_LastEditedByUserId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "EditorialNote",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "LastEditedAtUtc",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "LastEditedByUserId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ContentItems");
        }
    }
}
