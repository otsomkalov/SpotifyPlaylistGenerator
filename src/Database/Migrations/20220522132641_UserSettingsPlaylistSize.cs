using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    public partial class UserSettingsPlaylistSize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Settings_PlaylistSize",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 20);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings_PlaylistSize",
                table: "Users");
        }
    }
}
