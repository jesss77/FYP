using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnRestaurantTableNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_RestaurantID",
                table: "Tables");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RestaurantID_TableNumber",
                table: "Tables",
                columns: new[] { "RestaurantID", "TableNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_RestaurantID_TableNumber",
                table: "Tables");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RestaurantID",
                table: "Tables",
                column: "RestaurantID");
        }
    }
}
