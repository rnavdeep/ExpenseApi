namespace Expense.API.Models.DTO
{
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal MonthlyLimit { get; set; }
    }
}
