using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRecommendationStateAndCoordinates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Latitude",
            table: "Trips",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Longitude",
            table: "Trips",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsManual",
            table: "ProfilePackingListItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "RecommendationDecision",
            table: "ProfilePackingListItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "IsManual",
            table: "PackingListItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "RecommendationDecision",
            table: "PackingListItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Latitude",
            table: "Trips");

        migrationBuilder.DropColumn(
            name: "Longitude",
            table: "Trips");

        migrationBuilder.DropColumn(
            name: "IsManual",
            table: "ProfilePackingListItems");

        migrationBuilder.DropColumn(
            name: "RecommendationDecision",
            table: "ProfilePackingListItems");

        migrationBuilder.DropColumn(
            name: "IsManual",
            table: "PackingListItems");

        migrationBuilder.DropColumn(
            name: "RecommendationDecision",
            table: "PackingListItems");
    }
}
