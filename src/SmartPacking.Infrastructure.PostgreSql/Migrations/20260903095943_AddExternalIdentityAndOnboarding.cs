using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPacking.Infrastructure.PostgreSql.Migrations;

public partial class AddExternalIdentityAndOnboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ExternalIssuer", table: "Users", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ExternalSubject", table: "Users", type: "text", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsOnboarded", table: "Users", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.CreateIndex(name: "IX_Users_ExternalIssuer_ExternalSubject", table: "Users", columns: ["ExternalIssuer", "ExternalSubject"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_ExternalIssuer_ExternalSubject", table: "Users");
        migrationBuilder.DropColumn(name: "ExternalIssuer", table: "Users");
        migrationBuilder.DropColumn(name: "ExternalSubject", table: "Users");
        migrationBuilder.DropColumn(name: "IsOnboarded", table: "Users");
    }
}
