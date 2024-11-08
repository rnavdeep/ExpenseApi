using System;
namespace Expense.API.Models.DTO
{
	public class UserDto
	{
		public UserDto()
		{
		}
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}

