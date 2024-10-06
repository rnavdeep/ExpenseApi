using System;
using System.Threading.Tasks;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using ExpenseModel = Expense.API.Models.Domain.Expense;

namespace Expense.API.Repositories.Documents
{
	public interface IDocumentRepository
	{
        public Task<Document?> UploadFileAsync(DocumentDto documentDto,IFormFile file);
        public Task<string?> DownloadFileAsync(string fileName);
        public Task<List<string>> GetAllDownloadableLinksAsync();
        public Task<string?> StartExtractAsync(string fileName);
        public Task<string?> StartExpenseExtractAsync(string fileName);
        public Task<List<Document>> UploadFileFormAsync(IFormCollection files, ExpenseModel expense);
    }
}

