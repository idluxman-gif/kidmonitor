using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceToken",
                table: "Devices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceToken",
                table: "Devices",
                column: "DeviceToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_DeviceToken",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceToken",
                table: "Devices");
        }
    }
}
