using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoresFamilie.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Calendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleEventId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Start = table.Column<long>(type: "bigint", nullable: false),
                    End = table.Column<long>(type: "bigint", nullable: false),
                    AllDay = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Calendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OwnerMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoogleCalendarId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ConnectedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SyncToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastSyncedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleEventId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkCompletions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CalendarId_GoogleEventId",
                table: "CalendarEvents",
                columns: new[] { "CalendarId", "GoogleEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CalendarId_Start",
                table: "CalendarEvents",
                columns: new[] { "CalendarId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_FamilyId_GoogleCalendarId",
                table: "Calendars",
                columns: new[] { "FamilyId", "GoogleCalendarId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkCompletions_CalendarId_GoogleEventId",
                table: "HomeworkCompletions",
                columns: new[] { "CalendarId", "GoogleEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "Calendars");

            migrationBuilder.DropTable(
                name: "HomeworkCompletions");
        }
    }
}
