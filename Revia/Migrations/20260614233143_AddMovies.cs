using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class AddMovies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    Titel = table.Column<string>(type: "TEXT", nullable: true),
                    Jahr = table.Column<string>(type: "TEXT", nullable: true),
                    Fsk = table.Column<string>(type: "TEXT", nullable: true),
                    Kategorie = table.Column<string>(type: "TEXT", nullable: true),
                    Anbieter = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_HouseholdId",
                table: "Movies",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_HouseholdId_Titel",
                table: "Movies",
                columns: new[] { "HouseholdId", "Titel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movies");
        }
    }
}
