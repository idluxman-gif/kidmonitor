using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidMonitor.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCloudEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingCloudEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnqueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCloudEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingCloudEvents_EnqueuedAt",
                table: "PendingCloudEvents",
                column: "EnqueuedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingCloudEvents");
        }
    }
}
