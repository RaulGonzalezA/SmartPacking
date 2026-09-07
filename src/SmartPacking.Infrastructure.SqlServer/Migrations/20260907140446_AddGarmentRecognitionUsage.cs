using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SmartPacking.Infrastructure.SqlServer.Migrations;

/// <inheritdoc />
public partial class AddGarmentRecognitionUsage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GarmentRecognitionEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GarmentRecognitionEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GarmentRecognitionEvents_UserId_OccurredAt",
            table: "GarmentRecognitionEvents",
            columns: new[] { "UserId", "OccurredAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GarmentRecognitionEvents");
    }
}
