using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fibotrail.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotosAndScanRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    LocalTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    ImmichUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scan_run",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Cursor = table.Column<string>(type: "TEXT", nullable: true),
                    PhotosSeen = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangedSince = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LatestUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_run", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_photo_ImmichUpdatedAt",
                table: "photo",
                column: "ImmichUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_photo_LocalTime",
                table: "photo",
                column: "LocalTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo");

            migrationBuilder.DropTable(
                name: "scan_run");
        }
    }
}
