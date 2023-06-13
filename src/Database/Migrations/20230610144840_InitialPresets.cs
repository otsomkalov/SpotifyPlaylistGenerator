using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Migrations
{
    public partial class InitialPresets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings_IncludeLikedTracks",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Settings_PlaylistSize",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "CurrentPresetId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Presets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Settings_IncludeLikedTracks = table.Column<bool>(type: "boolean", nullable: true),
                    Settings_PlaylistSize = table.Column<int>(type: "integer", nullable: false, defaultValue: 20)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CurrentPresetId",
                table: "Users",
                column: "CurrentPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_Presets_UserId",
                table: "Presets",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Presets_CurrentPresetId",
                table: "Users",
                column: "CurrentPresetId",
                principalTable: "Presets",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Presets_CurrentPresetId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Presets");

            migrationBuilder.DropIndex(
                name: "IX_Users_CurrentPresetId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentPresetId",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "Settings_IncludeLikedTracks",
                table: "Users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Settings_PlaylistSize",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 20);
        }
    }
}
