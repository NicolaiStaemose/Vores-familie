using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoresFamilie.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShoppingPurchaseHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    BoughtOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_FamilyId_BoughtOn",
                table: "PurchaseRecords",
                columns: new[] { "FamilyId", "BoughtOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_ItemId",
                table: "PurchaseRecords",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseRecords");
        }
    }
}
