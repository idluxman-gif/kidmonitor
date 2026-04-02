using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidMonitor.Core.Data.Migrations;

/// <inheritdoc />
public partial class AddLanguageDetectionEvent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LanguageDetectionEvents",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ContentSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                AppName = table.Column<string>(type: "TEXT", nullable: false),
                Source = table.Column<string>(type: "TEXT", nullable: false),
                MatchedTerm = table.Column<string>(type: "TEXT", nullable: false),
                ContextSnippet = table.Column<string>(type: "TEXT", nullable: false),
                DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LanguageDetectionEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_LanguageDetectionEvents_ContentSessions_ContentSessionId",
                    column: x => x.ContentSessionId,
                    principalTable: "ContentSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LanguageDetectionEvents_ContentSessionId",
            table: "LanguageDetectionEvents",
            column: "ContentSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_LanguageDetectionEvents_DetectedAt",
            table: "LanguageDetectionEvents",
            column: "DetectedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LanguageDetectionEvents");
    }
}
