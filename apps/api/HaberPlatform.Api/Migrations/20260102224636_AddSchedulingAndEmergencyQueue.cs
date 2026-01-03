using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingAndEmergencyQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "PublishJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmergency",
                table: "PublishJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SilencePush",
                table: "PublishJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetPlatformsCsv",
                table: "PublishJobs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmergencyQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchedKeywordsCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DetectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetPlatformsCsv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OverrideSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyQueueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyQueueItems_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencyQueueItems_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyQueueItems_ContentItemId",
                table: "EmergencyQueueItems",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyQueueItems_ProcessedByUserId",
                table: "EmergencyQueueItems",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyQueueItems_Status",
                table: "EmergencyQueueItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyQueueItems_Status_Priority",
                table: "EmergencyQueueItems",
                columns: new[] { "Status", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyQueueItems");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "PublishJobs");

            migrationBuilder.DropColumn(
                name: "IsEmergency",
                table: "PublishJobs");

            migrationBuilder.DropColumn(
                name: "SilencePush",
                table: "PublishJobs");

            migrationBuilder.DropColumn(
                name: "TargetPlatformsCsv",
                table: "PublishJobs");
        }
    }
}
