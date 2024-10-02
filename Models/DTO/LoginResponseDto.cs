using System;
namespace Expense.API.Models.DTO
{
	public class LoginResponseDto
	{
		public LoginResponseDto()
		{
		}
		public bool IsLoggedIn { get; set; }
		public string Error { get; set; }
	}

}

