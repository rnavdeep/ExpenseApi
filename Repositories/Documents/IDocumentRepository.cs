using System;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;

namespace Expense.API.Repositories.Documents
{
	public interface IDocumentRepository
	{
        public Task<Document?> UploadFileAsync(DocumentDto documentDto,IFormFile file);
        public Task<string?> DownloadFileAsync(string fileName);
        public Task<List<string>> GetAllDownloadableLinksAsync();
        public Task<string?> StartExtractAsync(string fileName);
        public Task<string?> StartExpenseExtractAsync(string fileName);
        public Task<Document?> UploadFileFormAsync(IFormCollection files, Guid expenseId);
    }
}

