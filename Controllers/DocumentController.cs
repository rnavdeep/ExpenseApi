using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;
using NSWalks.API.Repositories;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace NSWalks.API.Controllers
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

            //Username exists or not
            var user = await userRepository.GetUserByEmail(document.UserName);
            if(user == null)
            {
                return StatusCode(500, "User does not exists");
            }

            // Call S3Service to upload file
            var result = await documentRepository.UploadFileAsync(document,document.File,user); 

            if (result == null)
            {
                return StatusCode(500, "Error occurred while uploading the file.");
            }
            return Ok(result.S3Url);

        }
        // Download File from S3
        [Authorize]
        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName, [FromForm] UserDto userDto)
        {
            // Call S3Service to download file
            var filePath = await documentRepository.DownloadFileAsync(fileName,userDto);

            if (filePath == null)
            {
                return NotFound();
            }

            // Get the file bytes from local storage
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // Return the file as a download to the user
            return File(fileBytes, "application/octet-stream", fileName);
        }

    }
}

