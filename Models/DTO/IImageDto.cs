using System;
using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
	public interface IDocDto
	{
        [Required]
        public IFormFile File { get; set; }
        public string? FileDescription { get; set; }
    }
    public class DocumentDto: IDocDto
    {
        private IFormFile _file;
        private string? _fileDescription;
        private Guid _expenseId;

        [Required]
        public IFormFile File { get => _file; set => _file = value; }
        public string? FileDescription { get => _fileDescription; set => _fileDescription = value; }
        [Required]
        public Guid ExpenseId { get => _expenseId; set => _expenseId = value; }
    }
}

