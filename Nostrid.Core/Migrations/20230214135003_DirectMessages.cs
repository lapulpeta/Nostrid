using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class DirectMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DmPairs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountL = table.Column<string>(type: "TEXT", nullable: false),
                    AccountH = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmPairs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DmPairs_AccountH",
                table: "DmPairs",
                column: "AccountH");

            migrationBuilder.CreateIndex(
                name: "IX_DmPairs_AccountL",
                table: "DmPairs",
                column: "AccountL");

            migrationBuilder.CreateIndex(
                name: "IX_DmPairs_AccountL_AccountH",
                table: "DmPairs",
                columns: new[] { "AccountL", "AccountH" },
                unique: true);

            migrationBuilder.Sql("DELETE FROM Events WHERE Kind = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DmPairs");
        }
    }
}
