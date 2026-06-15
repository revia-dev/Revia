using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddTvPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TvPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvPrograms_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvProgramEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SharedWithHousehold = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvProgramEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvProgramEntries_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TvProgramEntries_TvPrograms_TvProgramId",
                        column: x => x.TvProgramId,
                        principalTable: "TvPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TvProgramEntries_MovieId",
                table: "TvProgramEntries",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_TvProgramEntries_TvProgramId",
                table: "TvProgramEntries",
                column: "TvProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TvPrograms_HouseholdId_OwnerUserId",
                table: "TvPrograms",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TvPrograms_OwnerUserId",
                table: "TvPrograms",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TvProgramEntries");

            migrationBuilder.DropTable(
                name: "TvPrograms");
        }
    }
}
