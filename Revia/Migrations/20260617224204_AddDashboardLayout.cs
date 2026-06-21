using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DashboardLayout",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DashboardLayout",
                table: "AspNetUsers");
        }
    }
}
