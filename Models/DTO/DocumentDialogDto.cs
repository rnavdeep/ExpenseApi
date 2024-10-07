using System;
namespace Expense.API.Models.DTO
{
    public class DocumentDialogDto
    {
        public string Name { get; set; } 
        public string Url { get; set; } 

        public DocumentDialogDto(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }
}

