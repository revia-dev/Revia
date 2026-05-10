using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateLibrary.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LendingCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LendingCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Street = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    HouseNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LendingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LenderId = table.Column<int>(type: "INTEGER", nullable: false),
                    BorrowerId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoanDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LendingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LendingEntries_People_BorrowerId",
                        column: x => x.BorrowerId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LendingEntries_People_LenderId",
                        column: x => x.LenderId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LendingEntryCategories",
                columns: table => new
                {
                    LendingEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    LendingCategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LendingEntryCategories", x => new { x.LendingEntryId, x.LendingCategoryId });
                    table.ForeignKey(
                        name: "FK_LendingEntryCategories_LendingCategories_LendingCategoryId",
                        column: x => x.LendingCategoryId,
                        principalTable: "LendingCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LendingEntryCategories_LendingEntries_LendingEntryId",
                        column: x => x.LendingEntryId,
                        principalTable: "LendingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LendingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LendingEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LendingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LendingItems_LendingEntries_LendingEntryId",
                        column: x => x.LendingEntryId,
                        principalTable: "LendingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LendingCategories_Name",
                table: "LendingCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LendingEntries_BorrowerId",
                table: "LendingEntries",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_LendingEntries_DueDate",
                table: "LendingEntries",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_LendingEntries_LenderId_BorrowerId",
                table: "LendingEntries",
                columns: new[] { "LenderId", "BorrowerId" });

            migrationBuilder.CreateIndex(
                name: "IX_LendingEntries_LoanDate",
                table: "LendingEntries",
                column: "LoanDate");

            migrationBuilder.CreateIndex(
                name: "IX_LendingEntries_Status",
                table: "LendingEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LendingEntryCategories_LendingCategoryId",
                table: "LendingEntryCategories",
                column: "LendingCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LendingItems_LendingEntryId",
                table: "LendingItems",
                column: "LendingEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LendingItems_LendingEntryId_SortOrder",
                table: "LendingItems",
                columns: new[] { "LendingEntryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_People_LastName_FirstName",
                table: "People",
                columns: new[] { "LastName", "FirstName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LendingEntryCategories");

            migrationBuilder.DropTable(
                name: "LendingItems");

            migrationBuilder.DropTable(
                name: "LendingCategories");

            migrationBuilder.DropTable(
                name: "LendingEntries");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
