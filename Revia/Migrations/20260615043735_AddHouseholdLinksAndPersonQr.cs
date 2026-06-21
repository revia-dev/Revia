using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdLinksAndPersonQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonLinkToken",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "HouseholdLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdAId = table.Column<int>(type: "INTEGER", nullable: false),
                    HouseholdBId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdLinks_HouseholdAId_HouseholdBId",
                table: "HouseholdLinks",
                columns: new[] { "HouseholdAId", "HouseholdBId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdLinks");

            migrationBuilder.DropColumn(
                name: "PersonLinkToken",
                table: "AspNetUsers");
        }
    }
}
