using System;
namespace Expense.API.Models.DTO
{
	public class UploadedDocumentDto
	{
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }

        public UploadedDocumentDto(string id,string name, string url)
        {
            Id = id;
            Name = name;
            Url = url;
        }
        public UploadedDocumentDto()
        {

        }
    }
}

