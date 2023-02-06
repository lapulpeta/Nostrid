using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class Channels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToRootId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepostEventId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelDetails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    About = table.Column<string>(type: "TEXT", nullable: true),
                    PictureUrl = table.Column<string>(type: "TEXT", nullable: true),
                    DetailsLastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelDetails_Channels_Id",
                        column: x => x.Id,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_Kind_ChannelId",
                table: "Events",
                columns: new[] { "Kind", "ChannelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_Kind_ReplyToId",
                table: "Events",
                columns: new[] { "Kind", "ReplyToId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_Kind_ReplyToRootId",
                table: "Events",
                columns: new[] { "Kind", "ReplyToRootId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelDetails_Id",
                table: "ChannelDetails",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_Id",
                table: "Channels",
                column: "Id");

            migrationBuilder.Sql("DELETE FROM TagDatas");
            migrationBuilder.Sql("DELETE FROM Events");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelDetails");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_Events_Kind_ChannelId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_Kind_ReplyToId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_Kind_ReplyToRootId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ReplyToId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ReplyToRootId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RepostEventId",
                table: "Events");
        }
    }
}
