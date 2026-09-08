using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.Migrations;

public partial class ConvertOccurredAtToUnixMs : Migration
{
    private static readonly string[] columns = ["UserId", "OccurredAt"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // UserAuditEvents: copy to temp table with OccurredAt as INTEGER (unix ms)
        migrationBuilder.CreateTable(
            name: "_UserAuditEvents_tmp",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredAt = table.Column<long>(type: "INTEGER", nullable: false),
                Action = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK__UserAuditEvents_tmp", x => x.Id);
            });

        // Copy and convert existing TEXT datetime to unix ms
        migrationBuilder.Sql(@"INSERT INTO _UserAuditEvents_tmp (Id, UserId, OccurredAt, Action)
SELECT Id, UserId, CAST(strftime('%s', OccurredAt) * 1000 AS INTEGER), Action FROM UserAuditEvents;");

        migrationBuilder.DropTable(name: "UserAuditEvents");
        migrationBuilder.RenameTable(name: "_UserAuditEvents_tmp", newName: "UserAuditEvents");
        migrationBuilder.CreateIndex(
            name: "IX_UserAuditEvents_UserId_OccurredAt",
            table: "UserAuditEvents",
            columns: columns);

        // GarmentRecognitionEvents: same process
        migrationBuilder.CreateTable(
            name: "_GarmentRecognitionEvents_tmp",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredAt = table.Column<long>(type: "INTEGER", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK__GarmentRecognitionEvents_tmp", x => x.Id);
            });

        migrationBuilder.Sql(@"INSERT INTO _GarmentRecognitionEvents_tmp (Id, OccurredAt, UserId)
SELECT Id, CAST(strftime('%s', OccurredAt) * 1000 AS INTEGER), UserId FROM GarmentRecognitionEvents;");

        migrationBuilder.DropTable(name: "GarmentRecognitionEvents");
        migrationBuilder.RenameTable(name: "_GarmentRecognitionEvents_tmp", newName: "GarmentRecognitionEvents");
        migrationBuilder.CreateIndex(
            name: "IX_GarmentRecognitionEvents_UserId_OccurredAt",
            table: "GarmentRecognitionEvents",
            columns: columns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert UserAuditEvents to TEXT datetime
        migrationBuilder.CreateTable(
            name: "_UserAuditEvents_old",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredAt = table.Column<string>(type: "TEXT", nullable: false),
                Action = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK__UserAuditEvents_old", x => x.Id);
            });

        // Convert unix ms back to ISO8601-ish text (UTC)
        migrationBuilder.Sql(@"INSERT INTO _UserAuditEvents_old (Id, UserId, OccurredAt, Action)
SELECT Id, UserId, datetime(OccurredAt/1000, 'unixepoch'), Action FROM UserAuditEvents;");

        migrationBuilder.DropTable(name: "UserAuditEvents");
        migrationBuilder.RenameTable(name: "_UserAuditEvents_old", newName: "UserAuditEvents");
        migrationBuilder.CreateIndex(
            name: "IX_UserAuditEvents_UserId_OccurredAt",
            table: "UserAuditEvents",
            columns: columns);

        // Revert GarmentRecognitionEvents
        migrationBuilder.CreateTable(
            name: "_GarmentRecognitionEvents_old",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredAt = table.Column<string>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK__GarmentRecognitionEvents_old", x => x.Id);
            });

        migrationBuilder.Sql(@"INSERT INTO _GarmentRecognitionEvents_old (Id, OccurredAt, UserId)
SELECT Id, datetime(OccurredAt/1000, 'unixepoch'), UserId FROM GarmentRecognitionEvents;");

        migrationBuilder.DropTable(name: "GarmentRecognitionEvents");
        migrationBuilder.RenameTable(name: "_GarmentRecognitionEvents_old", newName: "GarmentRecognitionEvents");
        migrationBuilder.CreateIndex(
            name: "IX_GarmentRecognitionEvents_UserId_OccurredAt",
            table: "GarmentRecognitionEvents",
            columns: columns);
    }
}
