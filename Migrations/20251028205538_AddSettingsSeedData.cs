using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "SettingsID", "CreatedAt", "CreatedBy", "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Name", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Fine O Dine" },
                    { 2, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Opening Hours", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "10 AM - 10 PM" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "SettingsID",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "SettingsID",
                keyValue: 2);
        }
    }
}
