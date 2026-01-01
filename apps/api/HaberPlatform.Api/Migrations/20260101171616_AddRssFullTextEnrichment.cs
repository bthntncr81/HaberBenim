using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRssFullTextEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullTextExtractMode",
                table: "Sources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<bool>(
                name: "FullTextFetchEnabled",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArticleFetchError",
                table: "ContentItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHtml",
                table: "ContentItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentText",
                table: "ContentItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTruncated",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SummaryHtml",
                table: "ContentItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullTextExtractMode",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "FullTextFetchEnabled",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "ArticleFetchError",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "ContentHtml",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "ContentText",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "IsTruncated",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "SummaryHtml",
                table: "ContentItems");
        }
    }
}
