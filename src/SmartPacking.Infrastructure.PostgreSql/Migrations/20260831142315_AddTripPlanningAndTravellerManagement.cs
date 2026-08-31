using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable IDE0161 // EF Core generates block namespaces for migrations.
#pragma warning disable CA1861 // EF Core migration metadata uses array literals.

namespace SmartPacking.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddTripPlanningAndTravellerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CabinOnly",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LuggageAllowanceGrams",
                table: "Trips",
                type: "integer",
                nullable: false,
                defaultValue: 10000);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "FamilyProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyProfiles_UserId_IsArchived",
                table: "FamilyProfiles",
                columns: new[] { "UserId", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyProfiles_UserId_IsArchived",
                table: "FamilyProfiles");

            migrationBuilder.DropColumn(
                name: "CabinOnly",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LuggageAllowanceGrams",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "FamilyProfiles");
        }
    }
}
#pragma warning restore CA1861
#pragma warning restore IDE0161
