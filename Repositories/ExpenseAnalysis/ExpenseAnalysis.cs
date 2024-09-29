using System;
using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using DocumentModel = Expense.API.Models.Domain.Document;
using Formatting = Newtonsoft.Json.Formatting;
using S3Object = Amazon.Textract.Model.S3Object;
namespace Expense.API.Repositories.ExpenseAnalysis
{
    public class ExpenseAnalysis : IExpenseAnalysis
    {
        private readonly IConfiguration configuration;
        private readonly IAmazonS3 s3Client;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IAmazonTextract amazonTextract;

        public ExpenseAnalysis(IConfiguration configuration, IAmazonS3 amazonS3, IHttpContextAccessor httpContextAccessor
            , UserDocumentsDbContext userDocumentsDbContext, IAmazonTextract amazonTextract)
        {
            this.configuration = configuration;
            this.s3Client = amazonS3;
            this.httpContextAccessor = httpContextAccessor;
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.amazonTextract = amazonTextract;
        }

        public async Task<List<ExpenseDocumentResult>> StartExpenseExtractAsync(Guid expenseId)
        {
            string bucketName = configuration["AWS:BucketName"];
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                         .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not found.");
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == userName.ToLower());

            if (userFound == null)
            {
                throw new Exception("User does not exist.");
            }

            // Check if the expense exists for the logged-in user
            var expenseExists = await userDocumentsDbContext.Expenses
                .FirstOrDefaultAsync(exp => exp.Id == expenseId && exp.CreatedById == userFound.Id);

            if (expenseExists == null)
            {
                throw new Exception("Expense does not exist.");
            }

            // Find all the documents related to the expense
            List<DocumentModel> documentList = await userDocumentsDbContext.Documents
                .Where(doc => doc.ExpenseId == expenseId)
                .ToListAsync();

            if (!documentList.Any())
            {
                throw new Exception("No documents found for this expense.");
            }

            var resultList = new List<ExpenseDocumentResult>();

            foreach (var document in documentList)
            {
                string key = $"Documents/{userName}/{document.FileName}";

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
                                Name = $"Documents/{userName}/{document.FileName}"
                            }
                        }
                    };

                    try
                    {
                        var response = await amazonTextract.AnalyzeExpenseAsync(request);

                        // Create an ExpenseDocument object to store the result for this document
                        var expenseDoc = new ExpenseDocumentResult
                        {
                            DocumentName = document.FileName
                        };

                        // Populate summary fields
                        expenseDoc.SummaryFields = response.ExpenseDocuments
                            .SelectMany(expDoc => expDoc.SummaryFields)
                            .Select(field => new ExpenseSummaryField
                            {
                                FieldName = field.Type.Text,
                                FieldValue = field.ValueDetection.Text
                            }).ToList();

                        // Populate line items
                        expenseDoc.LineItems = response.ExpenseDocuments
                            .SelectMany(expDoc => expDoc.LineItemGroups)
                            .SelectMany(group => group.LineItems)
                            .Select(item => new Models.Domain.LineItem
                            {
                                Description = item.LineItemExpenseFields
                                                .FirstOrDefault(f => f.Type.Text == "ITEM")?.ValueDetection?.Text,
                                Quantity = item.LineItemExpenseFields
                                                .FirstOrDefault(f => f.Type.Text == "PRODUCT_CODE")?.ValueDetection?.Text,
                                Amount = item.LineItemExpenseFields
                                                .FirstOrDefault(f => f.Type.Text == "PRICE")?.ValueDetection?.Text
                            }).ToList();

                        resultList.Add(expenseDoc);
                    }
                    catch (AmazonTextractException textractException)
                    {
                        throw new Exception($"AWS Textract Error: {textractException.Message}");
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"General Error: {e.Message}");
                    }
                }
                else
                {
                    throw new Exception($"Document {document.FileName} does not exist in S3.");
                }
            }

            return resultList;
        }

    }
}

