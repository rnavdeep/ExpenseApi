using System;
namespace Expense.API.Models.DTO
{
	public class SessionDataDto
	{
        public string? UserName { get; set; }
        public bool IsLoggedIn { get; set; }

        public SessionDataDto(string? userName, bool isLoggedIn)
        {
            UserName = userName;
            IsLoggedIn = isLoggedIn;
        }
    }
}

