using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace packettransfer.App.Migrations
{
    /// <inheritdoc />
    public partial class AddDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Zone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeliveryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NationalCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssigneeUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageDeliveries_AssigneeUserId_Status",
                table: "PackageDeliveries",
                columns: new[] { "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageDeliveries_City",
                table: "PackageDeliveries",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDeliveries_DeliveryCode",
                table: "PackageDeliveries",
                column: "DeliveryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageDeliveries_IsDeleted",
                table: "PackageDeliveries",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDeliveries_NationalCode",
                table: "PackageDeliveries",
                column: "NationalCode");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDeliveries_Status",
                table: "PackageDeliveries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDeliveries");
        }
    }
}
