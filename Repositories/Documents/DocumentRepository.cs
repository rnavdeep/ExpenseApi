using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Expense.API.Data;
using Expense.API.Models.DTO;
using Document = Expense.API.Models.Domain.Document;
using Formatting = Newtonsoft.Json.Formatting;
using S3Object = Amazon.Textract.Model.S3Object;
using Expense.API.Repositories.Expense;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using ExpenseModel = Expense.API.Models.Domain.Expense;
using Expense.API.Models.Domain;
using Microsoft.AspNetCore.Http;

namespace Expense.API.Repositories.Documents
{
    public class DocumentRepository : IDocumentRepository
	{
        private readonly IConfiguration configuration;
        private readonly IAmazonS3 s3Client;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IAmazonTextract amazonTextract;
        private readonly IExpenseRepository expenseRepository;
        
        public DocumentRepository(IConfiguration configuration, IAmazonS3 amazonS3, IHttpContextAccessor httpContextAccessor
            ,UserDocumentsDbContext userDocumentsDbContext, IAmazonTextract amazonTextract, IExpenseRepository expenseRepository)
		{
            this.configuration = configuration;
            this.s3Client = amazonS3;
            this.httpContextAccessor = httpContextAccessor;
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.amazonTextract = amazonTextract;
            this.expenseRepository = expenseRepository;
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

        public async Task<Document> UploadDocumentDetailsAsync(Document doc)
        {

            await userDocumentsDbContext.Documents.AddAsync(doc);
            await userDocumentsDbContext.SaveChangesAsync();

            //Assign Document to User

            return doc;

        }

        public async Task<List<Document>> UploadDocumentDetailsAsync(List<Document> doc)
        {

            await userDocumentsDbContext.Documents.AddRangeAsync(doc);
            await userDocumentsDbContext.SaveChangesAsync();

            //Assign Document to User
            
            return doc;

        }
        public async Task<List<Document>> UploadFileFormAsync(IFormCollection formCollection, ExpenseModel expense)
        {
            // Retrieve the current logged-in user from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                                 .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not logged in");
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                                              .FirstOrDefaultAsync(u => u.Username.ToLower().Equals(userName.ToLower()));

            List<Document> documents = new List<Document>();
            if (userFound != null)
            {
                var bucketName = configuration["AWS:BucketName"];
                var uploadTasks = new List<Task<Document?>>();

                // Loop through each file in the formCollection
                foreach (var file in formCollection.Files)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        memoryStream.Position = 0; // Reset the stream position for reading

                        string fileName = file.FileName;
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        string extension = Path.GetExtension(fileName);

                        if (string.IsNullOrEmpty(fileNameWithoutExtension) || string.IsNullOrEmpty(extension))
                        {
                            continue;
                        }

                        var doc = new Document
                        {
                            UserId = userFound.Id,
                            Size = file.Length,
                            FileExtension = extension,
                            FileName = fileName,
                            Expense = expense,
                            ExpenseId = expense.Id
                        };

                        string newKey = $"Documents/{userName}/{expense.Id}/{fileName}";

                        var putRequest = new PutObjectRequest
                        {
                            BucketName = bucketName,
                            Key = newKey,
                            InputStream = memoryStream,  // Use memoryStream
                            ContentType = file.ContentType
                        };

                        // Execute the S3 upload
                        Amazon.S3.Model.PutObjectResponse resp = await s3Client.PutObjectAsync(putRequest);

                        var request = new GetPreSignedUrlRequest
                        {
                            BucketName = bucketName,
                            Key = newKey,
                            Expires = DateTime.UtcNow.AddDays(1),
                            Verb = HttpVerb.GET
                        };

                        doc.S3Url = s3Client.GetPreSignedURL(request);
                        doc.ETag = resp.ETag;
                        doc.VersionId = resp.VersionId;
                        documents.Add(doc);
                    }
                }
                return await UploadDocumentDetailsAsync(documents);
            }
            else
            {
                throw new Exception("Unable to upload document, Invalid user");
            }
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
                doc.Expense = await expenseRepository.GetExpenseByIdAsync(documentDto.ExpenseId);
                return doc;
                //return await UploadDocumentDetailsAsync(doc);
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

        public async Task<string?> StartExtractAsync(string fileName)
        {
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
            //userFound--> Check if the file provided exists
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
                        Prefix = key,
                    };

                    var versionsResponse = await s3Client.ListVersionsAsync(listVersionsRequest);

                    // Find the latest version of the file
                    var latestVersion = versionsResponse.Versions
                                          .Where(v => v.Key == key)
                                          .OrderByDescending(v => v.LastModified)
                                          .FirstOrDefault();

                    if (latestVersion != null)
                    {
                        var request = new AnalyzeDocumentRequest
                        {
                            Document = new Amazon.Textract.Model.Document
                            {
                                S3Object = new S3Object
                                {
                                    Bucket = bucketName, // Just the bucket name
                                    Name = $"Documents/{userName}/{fileName}" // Folder structure included in Name
                                }
                            },
                            FeatureTypes = new List<string> {"FORMS" }
                        };

                        try
                        {
                            var response = await amazonTextract.AnalyzeDocumentAsync(request);

                            // Prepare a structured result focusing on forms data
                            var result = new
                            {
                                FormData = response.Blocks
                                    .Where(block => block.BlockType == "KEY_VALUE_SET" && block.Confidence >= 90) // Filter for key-value sets
                                    .Select(block => new
                                    {
                                        block.Id,
                                        block.BlockType,
                                        Relationships = block.Relationships?.Select(rel => new
                                        {
                                            Type = rel.Type,
                                            Ids = rel.Ids,
                                            RelatedBlocks = rel.Ids.Select(id => response.Blocks.FirstOrDefault(b => b.Id == id))
                                                .Where(relatedBlock => relatedBlock != null)
                                                .Select(relatedBlock => new
                                                {
                                                    relatedBlock.Id,
                                                    relatedBlock.BlockType,
                                                    relatedBlock.Text
                                                })
                                        })
                                    })
                                    .ToList()
                            };

                            // Serialize the result to JSON
                            var jsonResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                            return jsonResult;
                        }
                        catch (AmazonTextractException textractException)
                        {
                            Console.WriteLine("AWS Textract Error: " + textractException.Message);
                            throw;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("General Exception: " + e.Message);
                            throw;
                        }

                    }
                    else
                    {
                        throw new Exception($"Document {fileName} does not exist for {userName}");
                    }
                }
                else
                {
                    throw new Exception($"Document {fileName} does not exist for {userName}");
                }
            }
            else
            {
                throw new Exception($"User Not Found");
            }
        }

        public async Task<string?> StartExpenseExtractAsync(string fileName)
        {
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
            //userFound--> Check if the file provided exists
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
                        Prefix = key,
                    };

                    var versionsResponse = await s3Client.ListVersionsAsync(listVersionsRequest);

                    // Find the latest version of the file
                    var latestVersion = versionsResponse.Versions
                                          .Where(v => v.Key == key)
                                          .OrderByDescending(v => v.LastModified)
                                          .FirstOrDefault();

                    if (latestVersion != null)
                    {
                        var request = new AnalyzeExpenseRequest
                        {
                            Document = new Amazon.Textract.Model.Document
                            {
                                S3Object = new S3Object
                                {
                                    Bucket = bucketName,
                                    Name = $"Documents/{userName}/{fileName}"
                                }
                            }
                        };

                        try
                        {
                            var response = await amazonTextract.AnalyzeExpenseAsync(request);

                            // Prepare a structured result for the expense analysis
                            var result = new
                            {
                                ExpenseDocuments = response.ExpenseDocuments.Select(expenseDoc => new
                                {
                                    SummaryFields = expenseDoc.SummaryFields,
                                    LineItems = expenseDoc.LineItemGroups
                                }).ToList()
                            };


                            // Serialize the result to JSON
                            var jsonResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                            return jsonResult;
                        }
                        catch (AmazonTextractException textractException)
                        {
                            Console.WriteLine("AWS Textract Error: " + textractException.Message);
                            throw;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("General Exception: " + e.Message);
                            throw;
                        }

                    }
                    else
                    {
                        throw new Exception($"Document {fileName} does not exist for {userName}");
                    }
                }
                else
                {
                    throw new Exception($"Document {fileName} does not exist for {userName}");
                }
            }
            else
            {
                throw new Exception($"User Not Found");
            }
        }

        public async Task<Document> UploadDocumentByExpenseId(Guid expenseId, IFormFile file)
        {
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                                  .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not logged in");
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                                              .FirstOrDefaultAsync(u => u.Username.ToLower().Equals(userName.ToLower()));
            // Check if the Expense exists in the database
            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(e => e.Id.Equals(expenseId));
            if (userFound != null && expense !=null)
            {
                var bucketName = configuration["AWS:BucketName"];
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0; // Reset the stream position for reading

                    string fileName = file.FileName;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);

                    if (string.IsNullOrEmpty(fileNameWithoutExtension) || string.IsNullOrEmpty(extension))
                    {
                        throw new Exception("U fked up");
                    }

                    var doc = new Document
                    {
                        UserId = userFound.Id,
                        Size = file.Length,
                        FileExtension = extension,
                        FileName = fileName,
                        Expense = expense,
                        ExpenseId = expense.Id
                    };

                    string newKey = $"Documents/{userName}/{expense.Id}/{fileName}";

                    var putRequest = new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = newKey,
                        InputStream = memoryStream,  // Use memoryStream
                        ContentType = file.ContentType
                    };

                    // Execute the S3 upload
                    Amazon.S3.Model.PutObjectResponse resp = await s3Client.PutObjectAsync(putRequest);

                    var request = new GetPreSignedUrlRequest
                    {
                        BucketName = bucketName,
                        Key = newKey,
                        Expires = DateTime.UtcNow.AddDays(1),
                        Verb = HttpVerb.GET
                    };

                    doc.S3Url = s3Client.GetPreSignedURL(request);
                    doc.ETag = resp.ETag;
                    doc.VersionId = resp.VersionId;
                    return await UploadDocumentDetailsAsync(doc);

                }
            }
            else
            {
                throw new Exception("Unable to upload document, Invalid user");
            }
        }

        public async Task<bool> DeleteDocumentByDocId(Guid docId)
        {
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                                            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not logged in");
            }
            try
            {

                var docLocalDb = await userDocumentsDbContext.Documents.FirstOrDefaultAsync();
                var bucketName = configuration["AWS:BucketName"];
                string key = $"Documents/{userName}/{docLocalDb.ExpenseId}/{docLocalDb.FileName}";
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                };

                await s3Client.DeleteObjectAsync(deleteRequest);
                userDocumentsDbContext.Remove(docLocalDb);
                return true;

            }
            catch(Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}

