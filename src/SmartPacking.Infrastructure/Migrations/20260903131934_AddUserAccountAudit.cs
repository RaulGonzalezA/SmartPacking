using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserAccountAudit : Migration
{
    private static readonly string[] userAuditEventIndexColumns = ["UserId", "OccurredAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserAuditEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Action = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserAuditEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserAuditEvents_UserId_OccurredAt",
            table: "UserAuditEvents",
            columns: userAuditEventIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserAuditEvents");
    }
}
