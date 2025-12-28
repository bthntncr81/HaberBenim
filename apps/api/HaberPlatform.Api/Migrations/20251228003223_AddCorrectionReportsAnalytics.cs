using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectionReportsAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VersionNo",
                table: "PublishJobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CurrentVersionNo",
                table: "ContentItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PublishOrigin",
                table: "ContentItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedByUserId",
                table: "ContentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNo",
                table: "ChannelPublishLogs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "DailyReportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportDateLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReportRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyReportRuns_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublishJobs_ContentItemId_VersionNo",
                table: "PublishJobs",
                columns: new[] { "ContentItemId", "VersionNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_PublishedByUserId",
                table: "ContentItems",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelPublishLogs_ContentItemId_Channel_VersionNo",
                table: "ChannelPublishLogs",
                columns: new[] { "ContentItemId", "Channel", "VersionNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelPublishLogs_Status_CreatedAtUtc",
                table: "ChannelPublishLogs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyReportRuns_CreatedByUserId",
                table: "DailyReportRuns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReportRuns_ReportDateLocal",
                table: "DailyReportRuns",
                column: "ReportDateLocal");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReportRuns_ReportDateLocal_Status",
                table: "DailyReportRuns",
                columns: new[] { "ReportDateLocal", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_Users_PublishedByUserId",
                table: "ContentItems",
                column: "PublishedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_Users_PublishedByUserId",
                table: "ContentItems");

            migrationBuilder.DropTable(
                name: "DailyReportRuns");

            migrationBuilder.DropIndex(
                name: "IX_PublishJobs_ContentItemId_VersionNo",
                table: "PublishJobs");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_PublishedByUserId",
                table: "ContentItems");

            migrationBuilder.DropIndex(
                name: "IX_ChannelPublishLogs_ContentItemId_Channel_VersionNo",
                table: "ChannelPublishLogs");

            migrationBuilder.DropIndex(
                name: "IX_ChannelPublishLogs_Status_CreatedAtUtc",
                table: "ChannelPublishLogs");

            migrationBuilder.DropColumn(
                name: "VersionNo",
                table: "PublishJobs");

            migrationBuilder.DropColumn(
                name: "CurrentVersionNo",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "PublishOrigin",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "VersionNo",
                table: "ChannelPublishLogs");
        }
    }
}
