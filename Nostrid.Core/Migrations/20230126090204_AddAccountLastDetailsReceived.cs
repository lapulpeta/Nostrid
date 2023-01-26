using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLastDetailsReceived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DetailsLastReceived",
                table: "AccountDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDetails_Id_DetailsLastReceived",
                table: "AccountDetails",
                columns: new[] { "Id", "DetailsLastReceived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountDetails_Id_DetailsLastReceived",
                table: "AccountDetails");

            migrationBuilder.DropColumn(
                name: "DetailsLastReceived",
                table: "AccountDetails");
        }
    }
}
