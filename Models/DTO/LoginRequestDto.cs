using System;
using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
	public class LoginRequestDto
	{
		public LoginRequestDto()
		{
		}
		[Required]
		[DataType(DataType.EmailAddress)]
		public string Username { get; set; }

		[Required]
		[DataType(DataType.Password)]
		public string Password { get; set; }
	}
}

