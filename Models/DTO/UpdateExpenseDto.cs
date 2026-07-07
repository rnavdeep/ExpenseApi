using System;
namespace Expense.API.Models.DTO
{
	public class UpdateExpenseDto
	{
		public UpdateExpenseDto(string id, string title, string description, string? category = null)
		{
			this.Id = id;
			this.Description = description;
			this.Title = title;
			this.Category = category;
		}
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string? Category { get; set; }
    }
}

