using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class WishlistItemPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "WishlistItems",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "WishlistItems");
        }
    }
}
