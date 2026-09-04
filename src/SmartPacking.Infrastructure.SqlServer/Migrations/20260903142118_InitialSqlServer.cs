using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable IDE0161, CA1861 // EF Core scaffolds migration syntax and constant column arrays.
#nullable disable

namespace SmartPacking.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPacked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClothingItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarmthLevel = table.Column<int>(type: "int", nullable: false),
                    Waterproof = table.Column<bool>(type: "bit", nullable: false),
                    Style = table.Column<int>(type: "int", nullable: false),
                    WeightGrams = table.Column<int>(type: "int", nullable: true),
                    IsClean = table.Column<bool>(type: "bit", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    PreferenceScore = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OwnerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CombinationIds = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClothingItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClothingUsage",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClothingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WasUsed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClothingUsage", x => new { x.TripId, x.ClothingItemId });
                });

            migrationBuilder.CreateTable(
                name: "FamilyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    PackingNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MedicalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackingListItems",
                columns: table => new
                {
                    PackingListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClothingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPacked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingListItems", x => new { x.PackingListId, x.ClothingItemId });
                });

            migrationBuilder.CreateTable(
                name: "PackingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfilePackingListItems",
                columns: table => new
                {
                    PackingListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClothingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPacked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePackingListItems", x => new { x.PackingListId, x.ClothingItemId });
                });

            migrationBuilder.CreateTable(
                name: "ProfilePackingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePackingLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TripProfiles",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripProfiles", x => new { x.TripId, x.ProfileId });
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MinimumTemperatureCelsius = table.Column<int>(type: "int", nullable: false),
                    MaximumTemperatureCelsius = table.Column<int>(type: "int", nullable: false),
                    Activities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LuggageAllowanceGrams = table.Column<int>(type: "int", nullable: false),
                    CabinOnly = table.Column<bool>(type: "bit", nullable: false),
                    LuggageType = table.Column<int>(type: "int", nullable: false),
                    LuggageHeightCentimetres = table.Column<int>(type: "int", nullable: false),
                    LuggageWidthCentimetres = table.Column<int>(type: "int", nullable: false),
                    LuggageDepthCentimetres = table.Column<int>(type: "int", nullable: false),
                    DayPlans = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AirlineCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransportTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Luggages = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalIssuer = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ExternalSubject = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsOnboarded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTripTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinimumTemperatureCelsius = table.Column<int>(type: "int", nullable: false),
                    MaximumTemperatureCelsius = table.Column<int>(type: "int", nullable: false),
                    LuggageAllowanceGrams = table.Column<int>(type: "int", nullable: false),
                    CabinOnly = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTripTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_UserId_TripId",
                table: "ChecklistItems",
                columns: new[] { "UserId", "TripId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClothingItems_UserId_IsDeleted",
                table: "ClothingItems",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ClothingItems_UserId_Name",
                table: "ClothingItems",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClothingItems_UserId_OwnerProfileId",
                table: "ClothingItems",
                columns: new[] { "UserId", "OwnerProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyProfiles_UserId_IsArchived",
                table: "FamilyProfiles",
                columns: new[] { "UserId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyProfiles_UserId_Name",
                table: "FamilyProfiles",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackingLists_UserId_TripId",
                table: "PackingLists",
                columns: new[] { "UserId", "TripId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePackingLists_UserId_TripId_ProfileId",
                table: "ProfilePackingLists",
                columns: new[] { "UserId", "TripId", "ProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripProfiles_UserId_TripId",
                table: "TripProfiles",
                columns: new[] { "UserId", "TripId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_UserId_StartDate",
                table: "Trips",
                columns: new[] { "UserId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAuditEvents_UserId_OccurredAt",
                table: "UserAuditEvents",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalIssuer_ExternalSubject",
                table: "Users",
                columns: new[] { "ExternalIssuer", "ExternalSubject" },
                unique: true,
                filter: "[ExternalIssuer] IS NOT NULL AND [ExternalSubject] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserTripTemplates_UserId_Name",
                table: "UserTripTemplates",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "ClothingItems");

            migrationBuilder.DropTable(
                name: "ClothingUsage");

            migrationBuilder.DropTable(
                name: "FamilyProfiles");

            migrationBuilder.DropTable(
                name: "PackingListItems");

            migrationBuilder.DropTable(
                name: "PackingLists");

            migrationBuilder.DropTable(
                name: "ProfilePackingListItems");

            migrationBuilder.DropTable(
                name: "ProfilePackingLists");

            migrationBuilder.DropTable(
                name: "TripProfiles");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "UserAuditEvents");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "UserTripTemplates");
        }
    }
}
