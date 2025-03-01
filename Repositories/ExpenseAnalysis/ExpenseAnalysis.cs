﻿using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using Azure;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Repositories.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;
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
        private readonly IHubContext<TextractNotificationHub> textractNotification;
        private readonly ITextractNotification textractNotificationDb;

        public ExpenseAnalysis(IConfiguration configuration, IAmazonS3 amazonS3, IHttpContextAccessor httpContextAccessor
            , UserDocumentsDbContext userDocumentsDbContext, IAmazonTextract amazonTextract,
                IHubContext<TextractNotificationHub> textractNotification, ITextractNotification textractNotificationDb)
        {
            this.configuration = configuration;
            this.s3Client = amazonS3;
            this.httpContextAccessor = httpContextAccessor;
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.amazonTextract = amazonTextract;
            this.textractNotification = textractNotification;
            this.textractNotificationDb = textractNotificationDb;
        }

        private string? BuildColumnJson(LineItemFields lineItem)
        {
            var headers = new List<Dictionary<string, string>>();
            var isFirstItem = true;

            foreach (var field in lineItem.LineItemExpenseFields)
            {

                var lineItemColumns = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(field.Type?.Text))
                {
                    // Dynamically add field types and values to the dictionary
                    lineItemColumns["key"] = field.Type.Text;
                    lineItemColumns["title"] = field.Type.Text;
                    if (isFirstItem)
                    {
                        lineItemColumns["align"] = "start";
                        isFirstItem = false;
                    }
                }
                headers.Add(lineItemColumns);
            }

            // Serialize the list of dictionaries to JSON
            string jsonResult = JsonConvert.SerializeObject(headers, Formatting.Indented);
            return jsonResult;
        }

        private string BuildLineItemJson(List<ExpenseDocument> expenseDocuments)
        {
            var lineItemsList = new List<Dictionary<string, string>>();

            foreach (var expenseDocument in expenseDocuments)
            {
                foreach (var lineItemGroup in expenseDocument.LineItemGroups)
                {
                    foreach (var lineItem in lineItemGroup.LineItems)
                    {
                        var lineItemFields = new Dictionary<string, string>();

                        foreach (var field in lineItem.LineItemExpenseFields)
                        {
                            if (!string.IsNullOrEmpty(field.Type?.Text) && !string.IsNullOrEmpty(field.ValueDetection?.Text))
                            {
                                // Dynamically add field types and values to the dictionary
                                lineItemFields[field.Type.Text] = field.ValueDetection.Text;
                            }
                        }

                        // Add the dictionary (representing a single line item) to the collection
                        lineItemsList.Add(lineItemFields);
                    }
                }
            }

            // Serialize the list of dictionaries to JSON
            string jsonResult = JsonConvert.SerializeObject(lineItemsList, Formatting.Indented);
            return jsonResult;
        }

        private string BuildSummaryFieldsJson(ExpenseDocument expenseDocument)
        {
            var summaryFieldsJson = new Dictionary<string, string>();


            foreach (var summaryGroup in expenseDocument.SummaryFields)
            {
                summaryFieldsJson[summaryGroup.Type.Text] = summaryGroup.ValueDetection.Text;
            }
            

            // Serialize the list of dictionaries to JSON
            string jsonResult = JsonConvert.SerializeObject(summaryFieldsJson, Formatting.Indented);
            return jsonResult;
        }

        private async Task UpdateTotal(Guid expenseId, Dictionary<string, string> summaryFields)
        {
            var expense = await userDocumentsDbContext.Expenses
                .Where(expense => expense.Id == expenseId)
                .FirstOrDefaultAsync();

            if (expense != null)
            {
                if (summaryFields.ContainsKey("TOTAL"))
                {
                    string fieldValue = summaryFields["TOTAL"];

                    // Try to parse the numeric value from the string as a decimal
                    if (decimal.TryParse(Regex.Replace(fieldValue, @"[^\d.-]", ""), out decimal amountAdded))
                    {
                        expense.Amount += amountAdded;

                        // Update the total and save changes
                        await userDocumentsDbContext.SaveChangesAsync();
                    }
                }
            }
        }

        private async Task UpdateExpenseUsers(Guid expenseId)
        {
            var expense = await userDocumentsDbContext.Expenses
                .Where(expense => expense.Id == expenseId)
                .FirstOrDefaultAsync();
 

            if(expense != null)
            {
                var expenseUsers = await userDocumentsDbContext.ExpenseUsers
                                .Where(expenseUser => expenseUser.ExpenseId == expenseId)
                                .ToListAsync();
                var expenseAmount = float.Parse(expense.Amount.ToString());
                foreach (var expenseUser in expenseUsers)
                {
                    expenseUser.UserAmount = Math.Round(expenseUser.UserShare * expenseAmount, 2);
                }
                await userDocumentsDbContext.SaveChangesAsync();
            }

        }

        public async Task StoreResults(GetExpenseAnalysisResponse getExpenseAnalysisResponse, DocumentJobResult documentJobResult, byte status)
        {
            documentJobResult.Total = 0;
            documentJobResult.ResultCreatedAt = DateTime.UtcNow;
            if (getExpenseAnalysisResponse.ExpenseDocuments[0].LineItemGroups[0].LineItems.Count > 0)
            {
                documentJobResult.ColumnNames = BuildColumnJson(getExpenseAnalysisResponse.ExpenseDocuments[0].LineItemGroups[0].LineItems[0]);
                documentJobResult.ResultLineItems = BuildLineItemJson(getExpenseAnalysisResponse.ExpenseDocuments);
            }
            documentJobResult.SummaryFields = BuildSummaryFieldsJson(getExpenseAnalysisResponse.ExpenseDocuments[0]);
            documentJobResult.Status = status;
            // Deserialize the JSON string back into a list of dictionaries
            var summaryFieldsList = JsonConvert.DeserializeObject<Dictionary<string, string>>(documentJobResult.SummaryFields);
            if(summaryFieldsList != null)
            {
                await UpdateTotal(documentJobResult.ExpenseId, summaryFieldsList);
            }
            await UpdateExpenseUsers(documentJobResult.ExpenseId);
            //await userDocumentsDbContext.DocumentJobResults.Up(result);
            await userDocumentsDbContext.SaveChangesAsync();

            //return result;
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
                string key = $"Documents/{userName}/{expenseId}/{document.FileName}";
                var request = new AnalyzeExpenseRequest
                {
                    Document = new Amazon.Textract.Model.Document
                    {
                        S3Object = new S3Object
                        {
                            Bucket = bucketName,
                            Name = $"Documents/{userName}/{expenseId}/{document.FileName}"
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
            }

            return resultList;
        }

        //public async Task<DocumentResult> StartExpenseExtractByDocIdAsync(Guid expenseId, Guid docId)
        //{
        //    string bucketName = configuration["AWS:BucketName"];
        //    var userName = httpContextAccessor.HttpContext?.User?.Claims
        //                 .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        //    if (string.IsNullOrEmpty(userName))
        //    {
        //        throw new Exception("User not found.");
        //    }

        //    // Check if the user exists in the database
        //    var userFound = await userDocumentsDbContext.Users
        //        .FirstOrDefaultAsync(u => u.Username.ToLower() == userName.ToLower());

        //    if (userFound == null)
        //    {
        //        throw new Exception("User does not exist.");
        //    }

        //    // Check if the expense exists for the logged-in user
        //    var expenseExists = await userDocumentsDbContext.Expenses
        //        .FirstOrDefaultAsync(exp => exp.Id == expenseId && exp.CreatedById == userFound.Id);

        //    if (expenseExists == null)
        //    {
        //        throw new Exception("Expense does not exist.");
        //    }

        //    // Find all the documents related to the expense
        //    DocumentModel? document = await userDocumentsDbContext.Documents
        //        .Where(doc => doc.Id == docId && doc.ExpenseId == expenseId)
        //        .FirstOrDefaultAsync();

        //    if (document != null)
        //    {
        //        string key = $"Documents/{userName}/{expenseId}/{document.FileName}";
        //        var request = new AnalyzeExpenseRequest
        //        {
        //            Document = new Amazon.Textract.Model.Document
        //            {
        //                S3Object = new S3Object
        //                {
        //                    Bucket = bucketName,
        //                    Name = key
        //                }
        //            }
        //        };
        //        try
        //        {
        //            var response = await amazonTextract.AnalyzeExpenseAsync(request);
        //            var result = new DocumentResult();

        //            //result.ExpenseId = expenseId;
        //            //result.DocumentId = docId;
        //            //result.CreatedById = userFound.Id;
        //            //result.Total = 0;
        //            //result.CreatedAt = DateTime.UtcNow;
        //            //result.ColumnNames = BuildColumnJson(response.ExpenseDocuments[0].LineItemGroups[0].LineItems[0]);
        //            //result.SummaryFields = BuildSummaryFieldsJson(response.ExpenseDocuments[0]);
        //            //result.ResultLineItems = BuildLineItemJson(response.ExpenseDocuments);
        //            //await userDocumentsDbContext.DocumentResult.AddAsync(result);
        //            //await userDocumentsDbContext.SaveChangesAsync();

        //            //return result;

             
        //        }
        //        catch (AmazonTextractException textractException)
        //        {
        //            throw new Exception($"AWS Textract Error: {textractException.Message}");
        //        }
        //    }
        //    else
        //    {
        //        throw new Exception("Document does not exist");

        //    }


        //    throw new Exception("Not Good");
        //}

        public async Task<string> StartExpenseExtractByDocIdJobIdAsync(Guid expenseId, Guid docId)
        {
            string bucketName = configuration["AWS:BucketName"];
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                         .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

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
            DocumentModel? document = await userDocumentsDbContext.Documents
                .Where(doc => doc.Id == docId && doc.ExpenseId == expenseId)
                .FirstOrDefaultAsync();
            if (document != null)
            {
                string key = $"Documents/{userName}/{expenseId}/{document.FileName}";
                var startDocumentAnalysisRequest = new StartExpenseAnalysisRequest
                {
                    DocumentLocation = new DocumentLocation
                    {
                        S3Object = new S3Object
                        {
                            Bucket = bucketName,
                            Name = key,
                            Version = document.VersionId
                        }
                    }
                };
                var c = await s3Client.GetObjectMetadataAsync(bucketName, key);

                try
                {
                    var startDocumentAnalysisResponse = await amazonTextract.StartExpenseAnalysisAsync(startDocumentAnalysisRequest);
                    var jobId = startDocumentAnalysisResponse.JobId;


                    //save job id in local db
                    DocumentJobResult documentJobResult = new DocumentJobResult();
                    documentJobResult.CreatedAt = DateTime.UtcNow;
                    documentJobResult.JobId = jobId;
                    documentJobResult.Status = 0;
                    documentJobResult.ExpenseId = expenseId;
                    documentJobResult.CreatedById = userFound.Id;
                    documentJobResult.DocumentId = docId;
                    await userDocumentsDbContext.DocumentJobResults.AddAsync(documentJobResult);
                    await userDocumentsDbContext.SaveChangesAsync();

                    //Notifications
                    string title = $"Expense: {documentJobResult.Expense.Title}";
                    string message = $"Processing: {documentJobResult.Document.FileName}. Result will be available soon.";
                    await textractNotificationDb.CreateNotifcation(documentJobResult.CreatedById, message, title, 0);
                    //send notification in hub
                    await textractNotification.Clients.User(userFound.Username.ToString()).SendAsync("TextractNotification", message);
                    return jobId;
                }
                catch (AmazonTextractException textractException)
                {
                    throw new Exception(textractException.Message);
                }
            }

            throw new Exception("Error Occured");

        }
    }
}

