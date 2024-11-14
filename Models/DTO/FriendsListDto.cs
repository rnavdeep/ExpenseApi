using System;
using System.Text.Json.Serialization;

namespace Expense.API.Models.DTO
{

    public class FriendsListDto
	{
        public required string Username { get; set; }
        public string Date => AcceptedAt.ToShortDateString();
        [JsonIgnore]
        public DateTime AcceptedAt { get; set; }
        public List<ExpenseDto> SharedExpenses { get; set; } = new List<ExpenseDto>();
    }
}

