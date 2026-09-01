using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable IDE0161 // EF Core generates block namespaces for migrations.
#pragma warning disable CA1861 // EF Core migration metadata uses array literals.

namespace SmartPacking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChecklistProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "ChecklistItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "ChecklistItems");
        }
    }
}
#pragma warning restore CA1861
#pragma warning restore IDE0161
