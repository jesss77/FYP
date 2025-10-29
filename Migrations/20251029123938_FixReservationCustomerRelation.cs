using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class FixReservationCustomerRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conditionally drop FK constraint if it exists
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Reservations_Customers_CustomerID1')
BEGIN
    ALTER TABLE [dbo].[Reservations] DROP CONSTRAINT [FK_Reservations_Customers_CustomerID1];
END
");

            // Conditionally drop index if it exists
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_Reservations_CustomerID1' 
      AND object_id = OBJECT_ID('dbo.Reservations')
)
BEGIN
    DROP INDEX [IX_Reservations_CustomerID1] ON [dbo].[Reservations];
END
");

            // Conditionally drop column if it exists
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'CustomerID1' 
      AND Object_ID = Object_ID(N'dbo.Reservations')
)
BEGIN
    ALTER TABLE [dbo].[Reservations] DROP COLUMN [CustomerID1];
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerID1",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CustomerID1",
                table: "Reservations",
                column: "CustomerID1");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Customers_CustomerID1",
                table: "Reservations",
                column: "CustomerID1",
                principalTable: "Customers",
                principalColumn: "CustomerID");
        }
    }
}
