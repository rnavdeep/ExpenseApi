using System;
using System.ComponentModel.DataAnnotations;

namespace NSWalks.API.Models.DTO
{
	public interface IDocDto
	{
        [Required]
        public IFormFile File { get; set; }
        public string? FileDescription { get; set; }
    }
    public class WalkImageDto : IDocDto
    {
        private IFormFile _file;
        private string _fileName;
        private string? _fileDescription;
        private string _walkCode;
        [Required]
        public IFormFile File { get => _file; set => _file = value; }
        [Required]
        public string FileName { get => _fileName; set => _fileName = value; }
        public string? FileDescription { get => _fileDescription; set => _fileDescription = value; }
        [Required]
        public string WalkCode { get => _walkCode; set => _walkCode = value; }
    }
    public class RegionImageDto : IDocDto
    {
        private IFormFile _file;
        private string _fileName;
        private string? _fileDescription;
        private string _regionCode;
        [Required]
        public IFormFile File { get => _file; set => _file = value; }
        [Required]
        public string FileName { get => _fileName; set => _fileName = value; }
        public string? FileDescription { get => _fileDescription; set => _fileDescription = value; }
        [Required]
        public string RegionCode { get => _regionCode; set => _regionCode = value; }
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

