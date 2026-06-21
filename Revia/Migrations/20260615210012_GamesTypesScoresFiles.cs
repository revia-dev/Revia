using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class GamesTypesScoresFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoreHigherIsBetter",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ScoreUnit",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "GameMatches");

            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "GameMatchParticipants",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreDefinitionId",
                table: "GameMatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameExpansionFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameExpansionId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameExpansionFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameExpansionFiles_GameExpansions_GameExpansionId",
                        column: x => x.GameExpansionId,
                        principalTable: "GameExpansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameScoreDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    HigherIsBetter = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameScoreDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameScoreDefinitions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTypeAssignments_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_ScoreDefinitionId",
                table: "GameMatches",
                column: "ScoreDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameExpansionFiles_GameExpansionId",
                table: "GameExpansionFiles",
                column: "GameExpansionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameScoreDefinitions_GameId",
                table: "GameScoreDefinitions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeAssignments_GameId_Type",
                table: "GameTypeAssignments",
                columns: new[] { "GameId", "Type" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GameMatches_GameScoreDefinitions_ScoreDefinitionId",
                table: "GameMatches",
                column: "ScoreDefinitionId",
                principalTable: "GameScoreDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameMatches_GameScoreDefinitions_ScoreDefinitionId",
                table: "GameMatches");

            migrationBuilder.DropTable(
                name: "GameExpansionFiles");

            migrationBuilder.DropTable(
                name: "GameScoreDefinitions");

            migrationBuilder.DropTable(
                name: "GameTypeAssignments");

            migrationBuilder.DropIndex(
                name: "IX_GameMatches_ScoreDefinitionId",
                table: "GameMatches");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "GameMatchParticipants");

            migrationBuilder.DropColumn(
                name: "ScoreDefinitionId",
                table: "GameMatches");

            migrationBuilder.AddColumn<bool>(
                name: "ScoreHigherIsBetter",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScoreUnit",
                table: "Games",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "GameMatches",
                type: "REAL",
                nullable: true);
        }
    }
}
