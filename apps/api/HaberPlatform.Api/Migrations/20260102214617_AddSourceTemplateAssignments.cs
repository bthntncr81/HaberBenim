using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaberPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceTemplateAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceTemplateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Auto"),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriorityOverride = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceTemplateAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceTemplateAssignments_PublishTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PublishTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceTemplateAssignments_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceTemplateAssignments_SourceId_Platform_IsActive",
                table: "SourceTemplateAssignments",
                columns: new[] { "SourceId", "Platform", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceTemplateAssignments_SourceId_Platform_TemplateId",
                table: "SourceTemplateAssignments",
                columns: new[] { "SourceId", "Platform", "TemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceTemplateAssignments_TemplateId",
                table: "SourceTemplateAssignments",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceTemplateAssignments");
        }
    }
}
