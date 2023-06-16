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

            migrationBuilder.DropForeignKey(
                name: "FK_Presets_Users_UserId",
                table: "Presets");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Presets_CurrentPresetId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CurrentPresetId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Presets_UserId",
                table: "Presets");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_UserId",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Presets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Playlists");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentPresetId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PresetId",
                table: "Playlists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CurrentPresetId",
                table: "Users",
                column: "CurrentPresetId",
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Presets_CurrentPresetId",
                table: "Users",
                column: "CurrentPresetId",
                principalTable: "Presets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Presets_PresetId",
                table: "Playlists");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Presets_CurrentPresetId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CurrentPresetId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_PresetId",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "PresetId",
                table: "Playlists");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentPresetId",
                table: "Users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Presets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Playlists",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CurrentPresetId",
                table: "Users",
                column: "CurrentPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_Presets_UserId",
                table: "Presets",
                column: "UserId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Presets_Users_UserId",
                table: "Presets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Presets_CurrentPresetId",
                table: "Users",
                column: "CurrentPresetId",
                principalTable: "Presets",
                principalColumn: "Id");
        }
    }
}
