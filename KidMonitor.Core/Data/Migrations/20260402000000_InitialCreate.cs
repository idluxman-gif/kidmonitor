using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidMonitor.Core.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppSessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ProcessName = table.Column<string>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ContentSessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AppSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                AppName = table.Column<string>(type: "TEXT", nullable: false),
                ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                ContentTitle = table.Column<string>(type: "TEXT", nullable: false),
                ContentIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                Channel = table.Column<string>(type: "TEXT", nullable: true),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContentSessions_AppSessions_AppSessionId",
                    column: x => x.AppSessionId,
                    principalTable: "AppSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "DailySummaries",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ReportDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                TotalScreenTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                AppBreakdownJson = table.Column<string>(type: "TEXT", nullable: false),
                FoulLanguageEventCount = table.Column<int>(type: "INTEGER", nullable: false),
                HtmlReportPath = table.Column<string>(type: "TEXT", nullable: true),
                GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailySummaries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ContentSnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ContentSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                AppName = table.Column<string>(type: "TEXT", nullable: false),
                ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                CapturedText = table.Column<string>(type: "TEXT", nullable: false),
                SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                Channel = table.Column<string>(type: "TEXT", nullable: true),
                CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentSnapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContentSnapshots_ContentSessions_ContentSessionId",
                    column: x => x.ContentSessionId,
                    principalTable: "ContentSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "NotificationLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Category = table.Column<string>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", nullable: false),
                Body = table.Column<string>(type: "TEXT", nullable: false),
                AppSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Delivered = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_NotificationLogs_AppSessions_AppSessionId",
                    column: x => x.AppSessionId,
                    principalTable: "AppSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppSessions_ProcessName",
            table: "AppSessions",
            column: "ProcessName");

        migrationBuilder.CreateIndex(
            name: "IX_AppSessions_StartedAt",
            table: "AppSessions",
            column: "StartedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ContentSessions_AppSessionId",
            table: "ContentSessions",
            column: "AppSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentSessions_StartedAt",
            table: "ContentSessions",
            column: "StartedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ContentSnapshots_CapturedAt",
            table: "ContentSnapshots",
            column: "CapturedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ContentSnapshots_ContentSessionId",
            table: "ContentSnapshots",
            column: "ContentSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_DailySummaries_ReportDate",
            table: "DailySummaries",
            column: "ReportDate",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NotificationLogs_AppSessionId",
            table: "NotificationLogs",
            column: "AppSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_NotificationLogs_SentAt",
            table: "NotificationLogs",
            column: "SentAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ContentSnapshots");
        migrationBuilder.DropTable(name: "ContentSessions");
        migrationBuilder.DropTable(name: "NotificationLogs");
        migrationBuilder.DropTable(name: "DailySummaries");
        migrationBuilder.DropTable(name: "AppSessions");
    }
}
