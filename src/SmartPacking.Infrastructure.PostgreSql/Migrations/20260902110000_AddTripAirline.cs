using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPacking.Infrastructure;

#nullable disable

namespace SmartPacking.Infrastructure.PostgreSql.Migrations;

[DbContext(typeof(SmartPackingDbContext))]
[Migration("20260902110000_AddTripAirline")]
public partial class AddTripAirline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "AirlineCode", table: "Trips", type: "text", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "AirlineCode", table: "Trips");
}
