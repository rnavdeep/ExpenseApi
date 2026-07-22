using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Expense.API.Migrations.UserDocumentsDb
{
    /// <inheritdoc />
    public partial class RenameBudgetsToCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A rename, not a drop+recreate — this must preserve existing rows.
            migrationBuilder.RenameTable(
                name: "Budgets",
                newName: "Categories");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Categories",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_Budgets_UserId_Category",
                table: "Categories",
                newName: "IX_Categories_UserId_Name");

            migrationBuilder.Sql("EXEC sp_rename N'PK_Budgets', N'PK_Categories', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_Budgets_Users_UserId', N'FK_Categories_Users_UserId', N'OBJECT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC sp_rename N'FK_Categories_Users_UserId', N'FK_Budgets_Users_UserId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_Categories', N'PK_Budgets', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories",
                newName: "IX_Budgets_UserId_Category");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Categories",
                newName: "Category");

            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "Budgets");
        }
    }
}
