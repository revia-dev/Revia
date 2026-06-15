using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddBmiRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BmiRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<string>(type: "TEXT", nullable: false),
                    MeasuredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<double>(type: "REAL", nullable: false),
                    HeightCm = table.Column<double>(type: "REAL", nullable: false),
                    Bmi = table.Column<double>(type: "REAL", nullable: false),
                    PhotoFileName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BmiRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BmiRecords_AspNetUsers_PersonId",
                        column: x => x.PersonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BmiRecords_HouseholdId_PersonId_MeasuredOn",
                table: "BmiRecords",
                columns: new[] { "HouseholdId", "PersonId", "MeasuredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_BmiRecords_PersonId",
                table: "BmiRecords",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BmiRecords");
        }
    }
}
