using System;
using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
	public class ExpenseFormDto
	{
        public string Title { get; set; }
        public string Description { get; set; }
        public List<DocDto> Files { get; set; }
    }
    public class DocDto
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string Type { get; set; }
    }
}

