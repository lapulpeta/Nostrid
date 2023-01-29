using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class FollowsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowList",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "FollowerList",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "Follows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    FollowId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_AccountId",
                table: "Follows",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_AccountId_FollowId",
                table: "Follows",
                columns: new[] { "AccountId", "FollowId" });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowId",
                table: "Follows",
                column: "FollowId");

            // Delete all follows and start again
            migrationBuilder.DeleteData("Events", "Kind", "3");
            migrationBuilder.Sql("UPDATE Accounts SET FollowsLastUpdate=NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Follows");

            migrationBuilder.AddColumn<string>(
                name: "FollowList",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FollowerList",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
