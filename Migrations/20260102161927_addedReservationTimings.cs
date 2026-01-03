using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class addedReservationTimings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedAt",
                table: "Reservations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedFor",
                table: "Reservations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ReservedFor",
                table: "Reservations");
        }
    }
}
