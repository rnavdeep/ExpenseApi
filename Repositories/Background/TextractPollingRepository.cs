using System;
using Amazon.Textract;
using Amazon.Textract.Model;
using Expense.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Expense.API.Repositories.Background
{
	public class TextractPollingRepository: BackgroundService, IBackgroundPollingRepository
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly AmazonTextractClient amazonTextract;
        private readonly UserDocumentsDbContext userDocumentsDbContext;

        public TextractPollingRepository(IServiceProvider serviceProvider, ILogger logger, AmazonTextractClient amazonTextract, UserDocumentsDbContext userDocumentsDbContext)
		{
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.amazonTextract = amazonTextract;
            this.userDocumentsDbContext = userDocumentsDbContext;
		}
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Textract polling service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    var pendingJobs = userDocumentsDbContext.DocumentJobResults.Where(job => job.Status == 0).ToList();

                    foreach (var job in pendingJobs)
                    {
                        // Poll the job
                        var isComplete = await PollTextractJob(job.JobId);

                        if (isComplete == 1)
                        {
                            // Mark job as completed in the database
                            job.Status = 1;
                            userDocumentsDbContext.SaveChanges();
                        }
                    }
                    

                    // Wait before checking again (for example, every minute)
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while polling Textract jobs.");
                }
            }

            logger.LogInformation("Textract polling service is stopping.");
        }
        public async Task<int> PollTextractJob(string jobId)
        {
            var getExpenseAnalysisRequest = new GetExpenseAnalysisRequest
            {
                JobId = jobId
            };
            var getExpenseAnalysisResponse = await amazonTextract.GetExpenseAnalysisAsync(getExpenseAnalysisRequest);
            if (getExpenseAnalysisResponse.JobStatus == JobStatus.SUCCEEDED)
            {
                // Process and store result

                // Build all the Json

                // Store result in DocumentResult Table

                //Update DocumentJobResult table with Status, DocumentResultId, ResultCreatedAt.

                return 1;
            }

            throw new NotImplementedException();
        }
    }
}

