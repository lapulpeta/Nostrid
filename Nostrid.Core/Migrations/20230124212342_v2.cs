using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedAtCurated",
                table: "Events",
                column: "CreatedAtCurated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CreatedAtCurated",
                table: "Events");
        }
    }
}
