using System;
namespace Expense.API.Models.DTO
{
	public class UpdateExpenseDto
	{
		public UpdateExpenseDto(string id, string title, string description)
		{
			this.Id = id;
			this.Description = description;
			this.Title = title;
		}
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}

