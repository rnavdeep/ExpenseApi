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
        /// <summary>
        /// Upload one document at a time
        /// </summary>
        /// <param name="expenseId"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public Task<Document> UploadDocumentByExpenseId(Guid expenseId,IFormFile file);
        /// <summary>
        /// Upload one document at a time
        /// </summary>
        /// <param name="docId"></param>
        /// <returns></returns>
        public Task<Boolean> DeleteDocumentByDocId(Guid docId);

    }
}

