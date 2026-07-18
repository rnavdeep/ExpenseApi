using System;
using System.Collections.Generic;

namespace Expense.API.Models.DTO
{
    public class LineItemDto
    {
        public Guid Id { get; set; }

        public string? Description { get; set; }

        public string? Quantity { get; set; }

        public decimal? Amount { get; set; }

        public int SortOrder { get; set; }

        public List<LineItemAssigneeDto> Assignees { get; set; } = new();
    }
}
