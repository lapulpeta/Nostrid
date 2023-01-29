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

            // Set all reads and writes to true, and switch priority (greater number is now higher priority)
            migrationBuilder.Sql("UPDATE Relays SET Priority = 10 - Priority, Read = 1, Write = 1");
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
