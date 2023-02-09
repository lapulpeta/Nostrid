using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class RelayFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Relays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxAutoRelays",
                table: "Configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Relays");

            migrationBuilder.DropColumn(
                name: "MaxAutoRelays",
                table: "Configs");
        }
    }
}
