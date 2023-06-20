using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    public partial class ImprovedPresets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Users_UserId",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_UserId",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Playlists");

            migrationBuilder.AddColumn<int>(
                name: "PresetId",
                table: "Playlists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_PresetId",
                table: "Playlists",
                column: "PresetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_Presets_PresetId",
                table: "Playlists",
                column: "PresetId",
                principalTable: "Presets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Presets_PresetId",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_PresetId",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "PresetId",
                table: "Playlists");

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Playlists",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_UserId",
                table: "Playlists",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_Users_UserId",
                table: "Playlists",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
