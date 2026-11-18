using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fibotrail.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoThumbhash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Thumbhash",
                table: "photo",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Thumbhash",
                table: "photo");
        }
    }
}
