using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storyboard.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFieldsToShot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioText",
                table: "Shots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GeneratedAudioPath",
                table: "Shots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TtsVoice",
                table: "Shots",
                type: "TEXT",
                nullable: false,
                defaultValue: "alloy");

            migrationBuilder.AddColumn<double>(
                name: "TtsSpeed",
                table: "Shots",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<string>(
                name: "TtsModel",
                table: "Shots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "AudioDuration",
                table: "Shots",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "GenerateAudio",
                table: "Shots",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioText",
                table: "Shots");

            migrationBuilder.DropColumn(
                name: "GeneratedAudioPath",
                table: "Shots");

            migrationBuilder.DropColumn(
                name: "TtsVoice",
                table: "Shots");

            migrationBuilder.DropColumn(
                name: "TtsSpeed",
                table: "Shots");

            migrationBuilder.DropColumn(
                name: "TtsModel",
                table: "Shots");

            migrationBuilder.DropColumn(
                name: "AudioDuration",
                table: "Shots");

            migrationBuilder.DropColumn(
                name: "GenerateAudio",
                table: "Shots");
        }
    }
}
