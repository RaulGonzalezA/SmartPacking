using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPacking.Infrastructure;

#nullable disable

namespace SmartPacking.Infrastructure.PostgreSql.Migrations;

[DbContext(typeof(SmartPackingDbContext))]
[Migration("20260902100000_AddTripItineraryAndLuggageProfile")]
public partial class AddTripItineraryAndLuggageProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "DayPlans", table: "Trips", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<int>(name: "LuggageDepthCentimetres", table: "Trips", type: "integer", nullable: false, defaultValue: 20);
        migrationBuilder.AddColumn<int>(name: "LuggageHeightCentimetres", table: "Trips", type: "integer", nullable: false, defaultValue: 55);
        migrationBuilder.AddColumn<int>(name: "LuggageType", table: "Trips", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>(name: "LuggageWidthCentimetres", table: "Trips", type: "integer", nullable: false, defaultValue: 40);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DayPlans", table: "Trips");
        migrationBuilder.DropColumn(name: "LuggageDepthCentimetres", table: "Trips");
        migrationBuilder.DropColumn(name: "LuggageHeightCentimetres", table: "Trips");
        migrationBuilder.DropColumn(name: "LuggageType", table: "Trips");
        migrationBuilder.DropColumn(name: "LuggageWidthCentimetres", table: "Trips");
    }
}
