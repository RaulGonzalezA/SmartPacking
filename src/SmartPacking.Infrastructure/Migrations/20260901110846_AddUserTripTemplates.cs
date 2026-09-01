using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTripTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTripTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Activities = table.Column<string>(type: "TEXT", nullable: false),
                    MinimumTemperatureCelsius = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumTemperatureCelsius = table.Column<int>(type: "INTEGER", nullable: false),
                    LuggageAllowanceGrams = table.Column<int>(type: "INTEGER", nullable: false),
                    CabinOnly = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTripTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTripTemplates_UserId_Name",
                table: "UserTripTemplates",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTripTemplates");
        }
    }
}
