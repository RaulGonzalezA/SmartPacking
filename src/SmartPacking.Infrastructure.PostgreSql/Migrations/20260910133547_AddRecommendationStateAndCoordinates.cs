using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.PostgreSql.Migrations
{
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
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Trips",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "ProfilePackingListItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecommendationDecision",
                table: "ProfilePackingListItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "PackingListItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecommendationDecision",
                table: "PackingListItems",
                type: "integer",
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
            => migrationBuilder.Sql($"ALTER TABLE \"{table}\" ALTER COLUMN \"OccurredAt\" TYPE bigint USING (EXTRACT(EPOCH FROM \"OccurredAt\") * 1000)::bigint;");

        private static void ConvertUnixMillisecondsToOccurredAt(MigrationBuilder migrationBuilder, string table)
            => migrationBuilder.Sql($"ALTER TABLE \"{table}\" ALTER COLUMN \"OccurredAt\" TYPE timestamp with time zone USING to_timestamp(\"OccurredAt\" / 1000.0);");
    }
}
