using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fibotrail.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameResultShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Share",
                table: "game_result",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Share",
                table: "game_result");
        }
    }
}
