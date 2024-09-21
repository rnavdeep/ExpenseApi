﻿using System;
using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NSWalks.API.Data;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;

namespace NSWalks.API.Repositories.Documents
{
    public class DocumentRepository : IDocumentRepository
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

        public async Task<string?> DownloadFileAsync(string fileName)
        {
            // Retrieve the current logged-in user from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                // Handle the case where no user is logged in
                return null;
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                                      .FirstOrDefaultAsync(u => u.Username.ToLower().Equals(userName.ToLower()));

            if (userFound != null)
            {
                string bucketName = configuration["AWS:BucketName"];
                string key = $"Documents/{userName}/{fileName}";

                // Check if the user has the document in the database
                var userHasDocument = await userDocumentsDbContext.Documents
                                          .FirstOrDefaultAsync(d => d.FileName == fileName && d.UserId == userFound.Id);

                if (userHasDocument != null)
                {
                    // Retrieve the latest version of the document from S3
                    var listVersionsRequest = new ListVersionsRequest
                    {
                        BucketName = bucketName,
                        Prefix = key
                    };

                    var versionsResponse = await s3Client.ListVersionsAsync(listVersionsRequest);

                    // Find the latest version of the file
                    var latestVersion = versionsResponse.Versions
                                          .Where(v => v.Key == key)
                                          .OrderByDescending(v => v.LastModified)
                                          .FirstOrDefault();

                    if (latestVersion != null)
                    {
                        // Create the GetObjectRequest with the specific version ID
                        var getRequest = new GetObjectRequest
                        {
                            BucketName = bucketName,
                            Key = key,
                            VersionId = latestVersion.VersionId // Retrieve the specific version
                        };

                        // Get the latest version of the file
                        using (var response = await s3Client.GetObjectAsync(getRequest))
                        {
                            // Prepare the local file path where the file will be saved
                            var localFilePath = Path.Combine($"Images/Documents/", fileName);

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

        public async Task<Document?> UploadFileAsync(DocumentDto documentDto,IFormFile file)
        {
            // Retrieve the current logged-in user from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                // Handle the case where no user is logged in
                throw new Exception("User not logged in");
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                                      .FirstOrDefaultAsync(u => u.Username.ToLower().Equals(userName.ToLower()));
            //user is logged In and found
            if (userFound != null)
            {
                var bucketName = configuration["AWS:BucketName"];
                string fileName = documentDto.File.FileName.ToString();

                //to be saved in local db
                var doc = new Document();

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
                string newKey = $"Documents/{userName}/{fileName}";

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

                    Amazon.S3.Model.PutObjectResponse resp = await s3Client.PutObjectAsync(uploadRequest);
                    // Generate S3 URL (Optional)
                    var s3Url = $"https://{bucketName}.s3.amazonaws.com/{newKey}";
                    doc.UserId = userFound.Id;
                    doc.Size = documentDto.File.Length;
                    doc.S3Url = s3Url;
                    doc.ETag = resp.ETag;
                    doc.VersionId = resp.VersionId;
                }
                return await UploadDocumentDetailsAsync(doc);
            }
            else
            {
                throw new Exception("Unable to upload document, Invalid user");
            }

        }

        public async Task<List<string>> GetAllDownloadableLinksAsync()
        {
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                return new List<string>(); // Return an empty list if no user is logged in
            }

            string bucketName = configuration["AWS:BucketName"];
            string userPrefix = $"Documents/{userName}/";

            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = userPrefix
            };

            var listResponse = await s3Client.ListObjectsV2Async(listRequest);
            var downloadLinks = new List<string>();

            foreach (var s3Object in listResponse.S3Objects)
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = s3Object.Key,
                    Expires = DateTime.UtcNow.AddHours(1) // URL valid for 1 hour
                };

                string url = s3Client.GetPreSignedURL(request);
                downloadLinks.Add(url);
            }

            return downloadLinks;
        }


    }
}
