using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable IDE0161

#nullable disable

namespace SmartPacking.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistedTransportPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransportPlan",
                table: "Trips",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransportPlan",
                table: "Trips");
        }
    }
}
