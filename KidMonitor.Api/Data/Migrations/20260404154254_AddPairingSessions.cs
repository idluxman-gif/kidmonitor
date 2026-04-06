using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPairingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PairingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PairingCode = table.Column<string>(type: "text", nullable: false),
                    DeviceKey = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PairingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PairingSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PairingSessions_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PairingSessions_DeviceId",
                table: "PairingSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PairingSessions_DeviceKey",
                table: "PairingSessions",
                column: "DeviceKey");

            migrationBuilder.CreateIndex(
                name: "IX_PairingSessions_ExpiresAt",
                table: "PairingSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PairingSessions_PairingCode",
                table: "PairingSessions",
                column: "PairingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PairingSessions_ParentId",
                table: "PairingSessions",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PairingSessions");
        }
    }
}
