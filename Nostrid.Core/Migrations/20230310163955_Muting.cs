using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class Muting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MutesLastUpdate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    MuteId = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_AccountId",
                table: "Mutes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_AccountId_MuteId",
                table: "Mutes",
                columns: new[] { "AccountId", "MuteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_MuteId",
                table: "Mutes",
                column: "MuteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mutes");

            migrationBuilder.DropColumn(
                name: "MutesLastUpdate",
                table: "Accounts");
        }
    }
}
