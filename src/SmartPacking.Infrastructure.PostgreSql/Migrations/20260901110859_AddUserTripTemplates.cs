using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.PostgreSql.Migrations;

#pragma warning disable CA1861 // EF-generated migration metadata.
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Activities = table.Column<string>(type: "text", nullable: false),
                    MinimumTemperatureCelsius = table.Column<int>(type: "integer", nullable: false),
                    MaximumTemperatureCelsius = table.Column<int>(type: "integer", nullable: false),
                    LuggageAllowanceGrams = table.Column<int>(type: "integer", nullable: false),
                    CabinOnly = table.Column<bool>(type: "boolean", nullable: false)
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
#pragma warning restore CA1861
