using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelConfigAndPublicPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "PublishedContents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "PublishedContents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "PublishToMobile",
                table: "ContentDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublishToWeb",
                table: "ContentDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublishToX",
                table: "ContentDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublishedContents_Path",
                table: "PublishedContents",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_PublishedContents_Slug",
                table: "PublishedContents",
                column: "Slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublishedContents_Path",
                table: "PublishedContents");

            migrationBuilder.DropIndex(
                name: "IX_PublishedContents_Slug",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "PublishedContents");

            migrationBuilder.DropColumn(
                name: "PublishToMobile",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "PublishToWeb",
                table: "ContentDrafts");

            migrationBuilder.DropColumn(
                name: "PublishToX",
                table: "ContentDrafts");
        }
    }
}
