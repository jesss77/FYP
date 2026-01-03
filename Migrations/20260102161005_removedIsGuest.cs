using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class removedIsGuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservation_CustomerOrGuest",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "Reservations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservation_CustomerOrGuest",
                table: "Reservations",
                sql: "((CustomerID IS NOT NULL AND GuestID IS NULL) OR (GuestID IS NOT NULL AND CustomerID IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservation_CustomerOrGuest",
                table: "Reservations");

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservation_CustomerOrGuest",
                table: "Reservations",
                sql: "([IsGuest] = 0 AND [CustomerID] IS NOT NULL AND [GuestID] IS NULL) OR ([IsGuest] = 1 AND [GuestID] IS NOT NULL AND [CustomerID] IS NULL)");
        }
    }
}
