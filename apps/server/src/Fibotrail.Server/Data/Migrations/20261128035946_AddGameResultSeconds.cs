using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fibotrail.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameResultSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Seconds",
                table: "game_result",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Seconds",
                table: "game_result");
        }
    }
}
