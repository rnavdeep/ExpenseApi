using System;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NSWalks.API.Data;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;

namespace NSWalks.API.Repositories
{
	public class DocumentRepository:IDocumentRepository
	{
        private readonly IConfiguration configuration;
        private readonly IAmazonS3 s3Client;
        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserDocumentsDbContext userDocumentsDbContext;

        public DocumentRepository(IConfiguration configuration, IAmazonS3 amazonS3, IWebHostEnvironment webHostEnvironment, IHttpContextAccessor httpContextAccessor
            ,UserDocumentsDbContext userDocumentsDbContext)
		{
            this.configuration = configuration;
            this.s3Client = amazonS3;
            this.httpContextAccessor = httpContextAccessor;
            this.webHostEnvironment = webHostEnvironment;
            this.userDocumentsDbContext = userDocumentsDbContext;
		}
        public async Task<GetObjectResponse?> FindDocumentS3Async(string? bucketName, string? key)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                };
                return await s3Client.GetObjectAsync(request);
            }
            catch (AmazonS3Exception e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null; // File does not exist
                }
                throw; // Re-throw other S3-related exceptions
            }
        }

        public async Task<string?> DownloadFileAsync(string fileName, UserDto userDto)
        {
            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                                  .FirstOrDefaultAsync(u => u.Username.ToLower().Equals(userDto.Username.ToLower()));

            if (userFound != null)
            {
                string bucketName = configuration["AWS:BucketName"];
                string key = $"Documents/{userDto.Username}/{fileName}";

                // Check if the user has the document in the database
                var userHasDocument = await userDocumentsDbContext.Documents
                                          .FirstOrDefaultAsync(d => d.FileName == fileName && d.UserId == userFound.Id);

                if (userHasDocument != null)
                {
                    // Retrieve the document from S3
                    var response = await FindDocumentS3Async(bucketName, key);
                    if (response != null)
                    {
                        // Prepare the local file path where the file will be saved
                        var localFilePath = Path.Combine($"Images/Documents/{userDto.Username}", fileName);

                        // Create a stream to download the file from S3
                        using (var responseStream = response.ResponseStream)
                        {
                            using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                            {
                                await responseStream.CopyToAsync(fileStream);
                            }
                        }

                        // Return the local file path after successfully downloading the file
                        return localFilePath;
                    }
                }
            }

            // Return null if the user or document was not found, or if there was an issue retrieving the file
            return null;
        }


        public async Task<Document?> UploadDocumentDetailsAsync(Document doc)
        {
            await userDocumentsDbContext.Documents.AddAsync(doc);
            await userDocumentsDbContext.SaveChangesAsync();

            //Assign Document to User
            return doc;
        }

        public async Task<Document?> UploadFileAsync(DocumentDto documentDto,IFormFile file,User user)
        {
            var bucketName = configuration["AWS:BucketName"];
            string fileName = documentDto.File.FileName.ToString();
            string key = $"Documents/{user.Username}/{fileName}";

            //to be saved in local db
            var doc = new Document();

            // Check if the file already exists in S3
            int version = 1;
            // Extract file name and extension once
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            // Ensure valid file name and extension
            if (string.IsNullOrEmpty(fileNameWithoutExtension) || string.IsNullOrEmpty(extension))
            {
                throw new ArgumentException("Invalid file name or extension.");
            }

            // Store file extension in the document object
            doc.FileExtension = extension;
            doc.FileName = fileName;
            // Initialize the key for the S3 object
            string newKey = $"Documents/{user.Username}/{fileName}";

            // Loop to find the next available version
            while (await FindDocumentS3Async(bucketName, newKey) != null)
            {
                // Increment version and update the file name
                string newFileName = $"{fileNameWithoutExtension}_v{version.ToString()}{extension}";
                doc.FileName = newFileName;

                // Update the key for the new version
                newKey = $"Documents/{user.Username}/{newFileName}";

                // Increment the version number for the next iteration if needed
                version++;
            }



            using (var newMemoryStream = new MemoryStream())
            {
                await file.CopyToAsync(newMemoryStream);

                var uploadRequest = new PutObjectRequest
                {
                    InputStream = newMemoryStream,
                    BucketName = bucketName,
                    Key = newKey,
                    ContentType = file.ContentType
                };

                await s3Client.PutObjectAsync(uploadRequest);
            }

            // Generate S3 URL (Optional)
            var s3Url = $"https://{bucketName}.s3.amazonaws.com/{newKey}";
            doc.UserId = user.Id;
            doc.Size = documentDto.File.Length;
            doc.S3Url = s3Url;
            return  await UploadDocumentDetailsAsync(doc);
        }
    }
}

