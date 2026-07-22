namespace Expense.API.Models.DTO
{
    public class CategoryStatusDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal MonthlyLimit { get; set; }
        public decimal Spent { get; set; }
    }
}
