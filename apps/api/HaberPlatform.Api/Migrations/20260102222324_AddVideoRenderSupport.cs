using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoRenderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutputType",
                table: "RenderJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Image");

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "RenderJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceVideoAssetId",
                table: "RenderJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "MediaAssets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "AdminAlerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "AdminAlerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_OutputType",
                table: "RenderJobs",
                column: "OutputType");

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_SourceVideoAssetId",
                table: "RenderJobs",
                column: "SourceVideoAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_RenderJobs_MediaAssets_SourceVideoAssetId",
                table: "RenderJobs",
                column: "SourceVideoAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RenderJobs_MediaAssets_SourceVideoAssetId",
                table: "RenderJobs");

            migrationBuilder.DropIndex(
                name: "IX_RenderJobs_OutputType",
                table: "RenderJobs");

            migrationBuilder.DropIndex(
                name: "IX_RenderJobs_SourceVideoAssetId",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "OutputType",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "SourceVideoAssetId",
                table: "RenderJobs");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "AdminAlerts");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "AdminAlerts");
        }
    }
}
