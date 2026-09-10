using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.SqlServer.Migrations;

/// <inheritdoc />
public partial class AddRecommendationStateAndCoordinates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ConvertOccurredAtToUnixMilliseconds(migrationBuilder, "UserAuditEvents");

        migrationBuilder.AddColumn<decimal>(
            name: "Latitude",
            table: "Trips",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Longitude",
            table: "Trips",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsManual",
            table: "ProfilePackingListItems",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "RecommendationDecision",
            table: "ProfilePackingListItems",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "IsManual",
            table: "PackingListItems",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "RecommendationDecision",
            table: "PackingListItems",
            type: "int",
            nullable: false,
            defaultValue: 0);

        ConvertOccurredAtToUnixMilliseconds(migrationBuilder, "GarmentRecognitionEvents");
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

        ConvertUnixMillisecondsToOccurredAt(migrationBuilder, "UserAuditEvents");
        ConvertUnixMillisecondsToOccurredAt(migrationBuilder, "GarmentRecognitionEvents");
    }

    private static void ConvertOccurredAtToUnixMilliseconds(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropIndex(name: $"IX_{table}_UserId_OccurredAt", table: table);
        migrationBuilder.AddColumn<long>(name: "OccurredAtUnix", table: table, type: "bigint", nullable: true);
        migrationBuilder.Sql($"UPDATE [{table}] SET [OccurredAtUnix] = DATEDIFF_BIG(MILLISECOND, CONVERT(datetimeoffset, '1970-01-01T00:00:00+00:00'), [OccurredAt]);");
        migrationBuilder.AlterColumn<long>(name: "OccurredAtUnix", table: table, type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.DropColumn(name: "OccurredAt", table: table);
        migrationBuilder.RenameColumn(name: "OccurredAtUnix", table: table, newName: "OccurredAt");
        migrationBuilder.CreateIndex(name: $"IX_{table}_UserId_OccurredAt", table: table, columns: ["UserId", "OccurredAt"]);
    }

    private static void ConvertUnixMillisecondsToOccurredAt(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropIndex(name: $"IX_{table}_UserId_OccurredAt", table: table);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "OccurredAtTimestamp", table: table, type: "datetimeoffset", nullable: true);
        migrationBuilder.Sql($"UPDATE [{table}] SET [OccurredAtTimestamp] = DATEADD(MILLISECOND, [OccurredAt] % 86400000, DATEADD(DAY, [OccurredAt] / 86400000, CONVERT(datetimeoffset, '1970-01-01T00:00:00+00:00')));");
        migrationBuilder.AlterColumn<DateTimeOffset>(name: "OccurredAtTimestamp", table: table, type: "datetimeoffset", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
        migrationBuilder.DropColumn(name: "OccurredAt", table: table);
        migrationBuilder.RenameColumn(name: "OccurredAtTimestamp", table: table, newName: "OccurredAt");
        migrationBuilder.CreateIndex(name: $"IX_{table}_UserId_OccurredAt", table: table, columns: ["UserId", "OccurredAt"]);
    }
}
