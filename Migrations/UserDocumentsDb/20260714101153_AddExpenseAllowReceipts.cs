using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Expense.API.Migrations.UserDocumentsDb
{
    /// <inheritdoc />
    public partial class AddExpenseAllowReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowReceipts",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowReceipts",
                table: "Expenses");
        }
    }
}
