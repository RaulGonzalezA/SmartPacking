using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable IDE0161

#nullable disable

namespace SmartPacking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAddressAndTripOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Trips",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Trips");
        }
    }
}
