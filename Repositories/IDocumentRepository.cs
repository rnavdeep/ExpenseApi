﻿using System;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;

namespace NSWalks.API.Repositories
{
	public interface IDocumentRepository
	{
        public Task<Document?> UploadFileAsync(DocumentDto documentDto,IFormFile file, User user);
        public Task<string?> DownloadFileAsync(string fileName, UserDto userDto);
    }
}
