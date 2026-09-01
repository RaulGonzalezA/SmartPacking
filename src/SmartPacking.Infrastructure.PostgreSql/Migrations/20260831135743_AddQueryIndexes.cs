using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // EF Core generated migration uses block-scoped namespaces.
#pragma warning disable CA1861 // EF Core generated migration creates index column arrays.

namespace SmartPacking.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Trips_UserId_StartDate",
                table: "Trips",
                columns: new[] { "UserId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TripProfiles_UserId_TripId",
                table: "TripProfiles",
                columns: new[] { "UserId", "TripId" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyProfiles_UserId_Name",
                table: "FamilyProfiles",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClothingItems_UserId_IsDeleted",
                table: "ClothingItems",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ClothingItems_UserId_OwnerProfileId",
                table: "ClothingItems",
                columns: new[] { "UserId", "OwnerProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_UserId_TripId",
                table: "ChecklistItems",
                columns: new[] { "UserId", "TripId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_UserId_StartDate",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_TripProfiles_UserId_TripId",
                table: "TripProfiles");

            migrationBuilder.DropIndex(
                name: "IX_FamilyProfiles_UserId_Name",
                table: "FamilyProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ClothingItems_UserId_IsDeleted",
                table: "ClothingItems");

            migrationBuilder.DropIndex(
                name: "IX_ClothingItems_UserId_OwnerProfileId",
                table: "ClothingItems");

            migrationBuilder.DropIndex(
                name: "IX_ChecklistItems_UserId_TripId",
                table: "ChecklistItems");
        }
    }
}
