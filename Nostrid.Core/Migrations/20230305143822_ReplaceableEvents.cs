using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceableEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplaceableId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_ReplaceableId",
                table: "Events",
                column: "ReplaceableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_ReplaceableId_CreatedAt",
                table: "Events",
                columns: new[] { "ReplaceableId", "CreatedAt" });

            // Remove all events that have an 'a' tag because we might have to recalculate their replyTo
			migrationBuilder.Sql("DELETE FROM Events WHERE Id IN (SELECT EventId FROM TagDatas WHERE Data0='a')");
			migrationBuilder.Sql("DELETE FROM TagDatas WHERE Data0='a'");

		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_ReplaceableId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_ReplaceableId_CreatedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ReplaceableId",
                table: "Events");
        }
    }
}
