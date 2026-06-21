using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    MinPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ScoreUnit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    ScoreHigherIsBetter = table.Column<bool>(type: "INTEGER", nullable: false),
                    RulesFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    RulesContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RulesOriginalName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameExpansions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameExpansions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameExpansions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    PlayedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameMatches_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameMatchParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameMatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMatchParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameMatchParticipants_AspNetUsers_PersonId",
                        column: x => x.PersonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameMatchParticipants_GameMatches_GameMatchId",
                        column: x => x.GameMatchId,
                        principalTable: "GameMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameMatchPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameMatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMatchPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameMatchPhotos_GameMatches_GameMatchId",
                        column: x => x.GameMatchId,
                        principalTable: "GameMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameExpansions_GameId",
                table: "GameExpansions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_GameId",
                table: "GameMatches",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_HouseholdId_StartedAt",
                table: "GameMatches",
                columns: new[] { "HouseholdId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchParticipants_GameMatchId_PersonId",
                table: "GameMatchParticipants",
                columns: new[] { "GameMatchId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchParticipants_PersonId",
                table: "GameMatchParticipants",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchPhotos_GameMatchId",
                table: "GameMatchPhotos",
                column: "GameMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_HouseholdId_Name",
                table: "Games",
                columns: new[] { "HouseholdId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameExpansions");

            migrationBuilder.DropTable(
                name: "GameMatchParticipants");

            migrationBuilder.DropTable(
                name: "GameMatchPhotos");

            migrationBuilder.DropTable(
                name: "GameMatches");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
