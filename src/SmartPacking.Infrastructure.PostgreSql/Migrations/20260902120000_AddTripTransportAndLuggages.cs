using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.PostgreSql.Migrations;

[DbContext(typeof(SmartPackingDbContext))]
[Migration("20260902120000_AddTripTransportAndLuggages")]
public partial class AddTripTransportAndLuggages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Luggages", table: "Trips", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>(name: "TransportTypes", table: "Trips", type: "text", nullable: false, defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Luggages", table: "Trips");
        migrationBuilder.DropColumn(name: "TransportTypes", table: "Trips");
    }
}
