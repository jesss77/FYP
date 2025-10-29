using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class FixReservationTablesRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationTables_Tables_TableID1",
                table: "ReservationTables");

            migrationBuilder.DropIndex(
                name: "IX_ReservationTables_TableID1",
                table: "ReservationTables");

            migrationBuilder.DropColumn(
                name: "TableID1",
                table: "ReservationTables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TableID1",
                table: "ReservationTables",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationTables_TableID1",
                table: "ReservationTables",
                column: "TableID1");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationTables_Tables_TableID1",
                table: "ReservationTables",
                column: "TableID1",
                principalTable: "Tables",
                principalColumn: "TableID");
        }
    }
}
