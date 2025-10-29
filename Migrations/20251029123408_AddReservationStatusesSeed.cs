using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationStatusesSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ReservationStatuses",
                columns: new[] { "ReservationStatusID", "CreatedAt", "CreatedBy", "Description", "StatusName", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Awaiting confirmation", "Pending", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system" },
                    { 2, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Confirmed by staff/system", "Confirmed", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system" },
                    { 3, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Customer seated", "Seated", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system" },
                    { 4, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Reservation completed", "Completed", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system" },
                    { 5, new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system", "Cancelled by customer/staff", "Cancelled", new DateTime(2025, 10, 16, 12, 0, 0, 0, DateTimeKind.Unspecified), "system" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ReservationStatuses",
                keyColumn: "ReservationStatusID",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ReservationStatuses",
                keyColumn: "ReservationStatusID",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ReservationStatuses",
                keyColumn: "ReservationStatusID",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ReservationStatuses",
                keyColumn: "ReservationStatusID",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "ReservationStatuses",
                keyColumn: "ReservationStatusID",
                keyValue: 5);
        }
    }
}
