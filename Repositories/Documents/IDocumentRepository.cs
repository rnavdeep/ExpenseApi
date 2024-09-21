using System;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;

namespace NSWalks.API.Repositories.Documents
{
	public interface IDocumentRepository
	{
        public Task<Document?> UploadFileAsync(DocumentDto documentDto,IFormFile file);
        public Task<string?> DownloadFileAsync(string fileName);
        public Task<List<string>> GetAllDownloadableLinksAsync();
    }
}

