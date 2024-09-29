using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Documents;
using Expense.API.Repositories.Users;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    public class DocumentController : Controller
    {
        private readonly IDocumentRepository documentRepository;
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        public DocumentController(IDocumentRepository documentRepository, IUserRepository userRepository,IMapper mapper)
        {
            this.documentRepository = documentRepository;
            this.userRepository = userRepository;
            this.mapper = mapper;
        }
        #region Validation
        private void CheckExtension(string fileName)
        {
            var allowedExtension = new string[] { ".jpg", ".jpeg", ".png",".pdf",".docx" };
            //check extensions
            if (allowedExtension.Contains(Path.GetExtension(fileName)) == false)
            {
                ModelState.AddModelError("File", "Unsupported Image Type");
            }
        }
        private void ValidateFileUpload(string fileName, long fileLength)
        {
            CheckExtension(fileName);
            //check filesize
            if (fileLength > 10485760)
            {
                ModelState.AddModelError("File", "Unsupported File Size");
            }

        }
        #endregion

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] DocumentDto document)
        {
            ValidateFileUpload(document.File.FileName, document.File.Length);

            if (document.File == null || document.File.Length == 0)
            {
                return BadRequest("File is required.");
            }
            try
            {
                // Call S3Service to upload file
                var result = await documentRepository.UploadFileAsync(document, document.File);
                if(result != null)
                {
                    return Ok(result.S3Url);

                }
                return BadRequest("upload failed");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }


        }
        // Download File from S3
        [Authorize]
        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            // Call S3Service to download file
            var filePath = await documentRepository.DownloadFileAsync(fileName);

            if (filePath == null)
            {
                return NotFound();
            }

            // Get the file bytes from local storage
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // Return the file as a download to the user
            return File(fileBytes, "application/octet-stream", fileName);
        }

        // Download File from S3
        [Authorize]
        [HttpGet("downloadLinks")]
        public async Task<IActionResult> GetAllDownloadableLinks()
        {
            // Get all download links for the current logged-in user
            var downloadLinks = await documentRepository.GetAllDownloadableLinksAsync();

            if (downloadLinks == null)
            {
                // Return 401 Unauthorized if no user is logged in
                return Unauthorized("User is not logged in.");
            }

            if (downloadLinks.Count == 0)
            {
                // Return 404 Not Found if the user has no documents
                return NotFound("No documents found for the user.");
            }

            // Return the list of downloadable links as a 200 OK response
            return Ok(downloadLinks);
        }

        [Authorize]
        [HttpPost("startTextract")]
        public async Task<IActionResult> StartTextractAsync(string fileName, string type)
        {
            try
            {
                if (type.Equals("Receipt"))
                {
                    var result = await documentRepository.StartExpenseExtractAsync(fileName);
                    return Ok(result);
                }
                else
                {
                    var result = await documentRepository.StartExtractAsync(fileName);
                    return Ok(result);
                }


            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}

