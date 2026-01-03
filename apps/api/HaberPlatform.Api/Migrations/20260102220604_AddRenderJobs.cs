using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRenderJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenderJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Queued"),
                    OutputMediaAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedTextSpecJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RenderJobs_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RenderJobs_MediaAssets_OutputMediaAssetId",
                        column: x => x.OutputMediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RenderJobs_PublishTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PublishTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_ContentItemId",
                table: "RenderJobs",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_ContentItemId_Platform_Status",
                table: "RenderJobs",
                columns: new[] { "ContentItemId", "Platform", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_OutputMediaAssetId",
                table: "RenderJobs",
                column: "OutputMediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_Status",
                table: "RenderJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_TemplateId",
                table: "RenderJobs",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenderJobs");
        }
    }
}
