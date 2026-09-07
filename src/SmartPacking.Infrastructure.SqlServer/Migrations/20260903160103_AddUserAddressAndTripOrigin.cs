using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable IDE0161

#nullable disable

namespace SmartPacking.Infrastructure.SqlServer.Migrations
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
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Trips",
                type: "nvarchar(max)",
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
