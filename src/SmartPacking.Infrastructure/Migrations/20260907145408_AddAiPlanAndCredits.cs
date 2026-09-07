using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAiPlanAndCredits : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AiPlan",
            table: "Users",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "AiRecognitionCredits",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AiPlan",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "AiRecognitionCredits",
            table: "Users");
    }
}
