using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakingFailoverComplianceRetract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRetracted",
                table: "PublishedContents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetractedAtUtc",
                table: "PublishedContents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAttributionText",
                table: "PublishedContents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BreakingAtUtc",
                table: "ContentItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BreakingByUserId",
                table: "ContentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BreakingNote",
                table: "ContentItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BreakingPriority",
                table: "ContentItems",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<bool>(
                name: "BreakingPushRequired",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBreaking",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetractReason",
                table: "ContentItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetractedAtUtc",
                table: "ContentItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetractedByUserId",
                table: "ContentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetaJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAlerts_Users_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SourceIngestionHealths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastFailureAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceIngestionHealths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceIngestionHealths_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublishedContents_IsRetracted",
                table: "PublishedContents",
                column: "IsRetracted");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_BreakingByUserId",
                table: "ContentItems",
                column: "BreakingByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_IsBreaking_BreakingPriority_BreakingAtUtc",
                table: "ContentItems",
                columns: new[] { "IsBreaking", "BreakingPriority", "BreakingAtUtc" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_RetractedByUserId",
                table: "ContentItems",
                column: "RetractedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_AcknowledgedByUserId",
                table: "AdminAlerts",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_CreatedAtUtc",
                table: "AdminAlerts",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_Severity_IsAcknowledged",
                table: "AdminAlerts",
                columns: new[] { "Severity", "IsAcknowledged" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_Type_CreatedAtUtc",
                table: "AdminAlerts",
                columns: new[] { "Type", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceIngestionHealths_SourceId",
                table: "SourceIngestionHealths",
                column: "SourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceIngestionHealths_Status",
                table: "SourceIngestionHealths",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_Users_BreakingByUserId",
                table: "ContentItems",
                column: "BreakingByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_Users_RetractedByUserId",
                table: "ContentItems",
                column: "RetractedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_Users_BreakingByUserId",
                table: "ContentItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_Users_RetractedByUserId",
                table: "ContentItems");

            migrationBuilder.DropTable(
                name: "AdminAlerts");

            migrationBuilder.DropTable(
                name: "SourceIngestionHealths");

            migrationBuilder.DropIndex(
                name: "IX_PublishedContents_IsRetracted",
                table: "PublishedContents");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_BreakingByUserId",
                table: "ContentItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_IsBreaking_BreakingPriority_BreakingAtUtc",
                table: "ContentItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_RetractedByUserId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "IsRetracted",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "RetractedAtUtc",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "SourceAttributionText",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "BreakingAtUtc",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "BreakingByUserId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "BreakingNote",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "BreakingPriority",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "BreakingPushRequired",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "IsBreaking",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "RetractReason",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "RetractedAtUtc",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "RetractedByUserId",
                table: "ContentItems");
        }
    }
}
