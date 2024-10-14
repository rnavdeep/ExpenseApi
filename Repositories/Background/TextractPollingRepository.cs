﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Textract;
using Amazon.Textract.Model;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Repositories.ExpenseAnalysis;
using Expense.API.Repositories.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Expense.API.Repositories.Background
{
    public class TextractPollingRepository : BackgroundService, IBackgroundPollingRepository
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<TextractPollingRepository> logger;
        private readonly IAmazonTextract amazonTextract;
        private readonly IHubContext<TextractNotificationHub> textractNotification;
        public TextractPollingRepository(IServiceProvider serviceProvider,
                                         ILogger<TextractPollingRepository> logger,
                                         IAmazonTextract amazonTextract,
                                         IHubContext<TextractNotificationHub> textractNotification)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.amazonTextract = amazonTextract;
            this.textractNotification = textractNotification;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Textract polling service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Use the service provider to create a scope
                    using (var scope = serviceProvider.CreateScope())
                    {
                        // Resolve the scoped DbContext and ExpenseAnalysis service within the scope
                        var userDocumentsDbContext = scope.ServiceProvider.GetRequiredService<UserDocumentsDbContext>();
                        var expenseAnalysis = scope.ServiceProvider.GetRequiredService<IExpenseAnalysis>();

                        // Fetch all pending jobs from the database
                        var pendingJobs = await userDocumentsDbContext.DocumentJobResults.Where(job=>job.Status == 0)
                            .ToListAsync();

                        foreach (var job in pendingJobs)
                        {
                            // Poll the job and update the result as necessary
                            await PollTextractJob(job.JobId, job, userDocumentsDbContext, expenseAnalysis, stoppingToken);
                        }
                    }

                    // Wait before polling again
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while polling Textract jobs.");
                }
            }

            logger.LogInformation("Textract polling service is stopping.");
        }

        public async Task PollTextractJob(string jobId, DocumentJobResult documentJobResult,
                                           UserDocumentsDbContext userDocumentsDbContext,
                                           IExpenseAnalysis expenseAnalysis,
                                           CancellationToken stoppingToken)
        {
            try
            {
                var getExpenseAnalysisRequest = new GetExpenseAnalysisRequest
                {
                    JobId = jobId
                };

                var getExpenseAnalysisResponse = await amazonTextract.GetExpenseAnalysisAsync(getExpenseAnalysisRequest);

                if (getExpenseAnalysisResponse.JobStatus == JobStatus.SUCCEEDED)
                {
                    // Process and store results using the expenseAnalysis service
                    await expenseAnalysis.StoreResults(getExpenseAnalysisResponse, documentJobResult, 1);
                }

                // Update job status in the database after processing
                documentJobResult.Status = 1; // Mark job as completed
                await userDocumentsDbContext.SaveChangesAsync(stoppingToken);
                string userId = documentJobResult.CreatedById.ToString();
                string message = $"Expense {documentJobResult.Expense.Title} is processed and result is ready to view.";
                await textractNotification.Clients.All.SendAsync("ReceiveMessage", message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to process Textract job with JobId: {jobId}");
                documentJobResult.Status = 2; // Mark job as failed
                await userDocumentsDbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }
}