namespace Expense.API.Models.DTO
{
    public class BudgetStatusDto
    {
        public string Category { get; set; }
        public decimal MonthlyLimit { get; set; }
        public decimal Spent { get; set; }
    }
}
