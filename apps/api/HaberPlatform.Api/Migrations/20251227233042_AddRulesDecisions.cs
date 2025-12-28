using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRulesDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Sources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrustLevel",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAtUtc",
                table: "ContentItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedByRuleId",
                table: "ContentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "ContentItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionType",
                table: "ContentItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAtUtc",
                table: "ContentItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrustLevelSnapshot",
                table: "ContentItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    DecisionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MinTrustLevel = table.Column<int>(type: "integer", nullable: true),
                    KeywordsIncludeCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    KeywordsExcludeCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceIdsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GroupIdsCsv = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Group",
                table: "Sources",
                column: "Group");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_DecidedByRuleId",
                table: "ContentItems",
                column: "DecidedByRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_DecisionType_PublishedAtUtc",
                table: "ContentItems",
                columns: new[] { "DecisionType", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_Status_PublishedAtUtc",
                table: "ContentItems",
                columns: new[] { "Status", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_CreatedByUserId",
                table: "Rules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_IsEnabled_Priority",
                table: "Rules",
                columns: new[] { "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Name",
                table: "Rules",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_Rules_DecidedByRuleId",
                table: "ContentItems",
                column: "DecidedByRuleId",
                principalTable: "Rules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_Rules_DecidedByRuleId",
                table: "ContentItems");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropIndex(
                name: "IX_Sources_Group",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_DecidedByRuleId",
                table: "ContentItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_DecisionType_PublishedAtUtc",
                table: "ContentItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_Status_PublishedAtUtc",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "TrustLevel",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "DecidedByRuleId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "DecisionType",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "ScheduledAtUtc",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "TrustLevelSnapshot",
                table: "ContentItems");
        }
    }
}
