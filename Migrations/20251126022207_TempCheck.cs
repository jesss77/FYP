using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class TempCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "SettingsID",
                keyValue: 2,
                column: "Value",
                value: "10:00 - 22:00");

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "SettingsID", "CreatedAt", "CreatedBy", "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { 3, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Phone", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "+1 555 123 4567" },
                    { 4, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Staff Number", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "SettingsID",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "SettingsID",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "SettingsID",
                keyValue: 2,
                column: "Value",
                value: "10 AM - 10 PM");
        }
    }
}
