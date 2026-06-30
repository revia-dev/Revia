using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revia.Migrations
{
    /// <inheritdoc />
    public partial class TvEntryOptionalMovie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "TvProgramEntries",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "CustomTitle",
                table: "TvProgramEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomTitle",
                table: "TvProgramEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "TvProgramEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
