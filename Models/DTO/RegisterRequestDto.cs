using System;
using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
	public class RegisterRequestDto
	{
		public RegisterRequestDto()
		{
		}

        [Required]
		[DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        [Required]
        public string UserName { get; set; }
        [Required]
		[DataType(DataType.Password)]
		public string Password { get; set; }

		public string[] Roles { get; set; }
    }
}

