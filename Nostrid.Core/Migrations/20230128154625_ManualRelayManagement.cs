using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class ManualRelayManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Read",
                table: "Relays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Write",
                table: "Relays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ManualRelayManagement",
                table: "Configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData("Relays", "Read", false, "Read", true); // Updates all read=false (default) to true
            migrationBuilder.UpdateData("Relays", "Write", false, "Write", true); // Updates all write=false (default) to true
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Read",
                table: "Relays");

            migrationBuilder.DropColumn(
                name: "Write",
                table: "Relays");

            migrationBuilder.DropColumn(
                name: "ManualRelayManagement",
                table: "Configs");
        }
    }
}
