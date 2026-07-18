using System;

namespace Expense.API.Models.DTO
{
    public class LineItemAssigneeDto
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; }
    }
}
