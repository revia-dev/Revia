using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Runtime",
                table: "Movies",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Runtime",
                table: "Movies");
        }
    }
}
