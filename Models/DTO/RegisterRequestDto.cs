using System;
using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
	public class RegisterRequestDto
	{
		public RegisterRequestDto()
		{
		}

		[DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        public string UserName { get; set; }
		[DataType(DataType.Password)]
		public string Password { get; set; }

		public string[] Roles { get; set; }
    }
}

