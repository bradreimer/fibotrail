using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fibotrail.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_result",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Game = table.Column<string>(type: "TEXT", nullable: false),
                    PuzzleDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Solved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tries = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxTries = table.Column<int>(type: "INTEGER", nullable: false),
                    Marks = table.Column<string>(type: "TEXT", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_result", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_result_Game_PuzzleDate",
                table: "game_result",
                columns: new[] { "Game", "PuzzleDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_result");
        }
    }
}
