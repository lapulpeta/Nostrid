using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nostrid.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PrivKey = table.Column<string>(type: "TEXT", nullable: true),
                    FollowList = table.Column<string>(type: "TEXT", nullable: false),
                    FollowerList = table.Column<string>(type: "TEXT", nullable: false),
                    FollowsLastUpdate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastNotificationRead = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShowDifficulty = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinDiffIncoming = table.Column<int>(type: "INTEGER", nullable: false),
                    StrictDiffCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                    TargetDiffOutgoing = table.Column<int>(type: "INTEGER", nullable: false),
                    MainAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    Theme = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PublicKey = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Signature = table.Column<string>(type: "TEXT", nullable: false),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtCurated = table.Column<long>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    HasPow = table.Column<bool>(type: "INTEGER", nullable: false),
                    Broadcast = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventSeen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    RelayId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSeen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Hashtags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Relays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uri = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountDetails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    About = table.Column<string>(type: "TEXT", nullable: true),
                    PictureUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Nip05Id = table.Column<string>(type: "TEXT", nullable: true),
                    Lud16Id = table.Column<string>(type: "TEXT", nullable: true),
                    Lud06Url = table.Column<string>(type: "TEXT", nullable: true),
                    Nip05Valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetailsLastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountDetails_Accounts_Id",
                        column: x => x.Id,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    TagIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Data0 = table.Column<string>(type: "TEXT", nullable: true),
                    Data1 = table.Column<string>(type: "TEXT", nullable: true),
                    Data2 = table.Column<string>(type: "TEXT", nullable: true),
                    Data3 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagDatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagDatas_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDetails_Id",
                table: "AccountDetails",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Id",
                table: "Accounts",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Id",
                table: "Events",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Kind",
                table: "Events",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_Events_PublicKey",
                table: "Events",
                column: "PublicKey");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeen_EventId_RelayId",
                table: "EventSeen",
                columns: new[] { "EventId", "RelayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagDatas_Data0",
                table: "TagDatas",
                column: "Data0");

            migrationBuilder.CreateIndex(
                name: "IX_TagDatas_Data0_Data1",
                table: "TagDatas",
                columns: new[] { "Data0", "Data1" });

            migrationBuilder.CreateIndex(
                name: "IX_TagDatas_EventId",
                table: "TagDatas",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDetails");

            migrationBuilder.DropTable(
                name: "Configs");

            migrationBuilder.DropTable(
                name: "EventSeen");

            migrationBuilder.DropTable(
                name: "FeedSources");

            migrationBuilder.DropTable(
                name: "Relays");

            migrationBuilder.DropTable(
                name: "TagDatas");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
