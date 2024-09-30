using System.Net;
using System.Text;
using Expense.API.Models.Domain;
using Expense.API.Repositories.Request;
using Newtonsoft.Json;

namespace Expense.API.Middlewares
{
	public class ExceptionHandlerMiddleware
	{
		private readonly ILogger<ExceptionHandlerMiddleware> logger;
		private readonly RequestDelegate next;
        private readonly IServiceProvider serviceProvider;

        public ExceptionHandlerMiddleware(ILogger<ExceptionHandlerMiddleware> logger, IServiceProvider serviceProvider,
            RequestDelegate next)
		{
			this.logger = logger;
			this.next = next;
            this.serviceProvider = serviceProvider;
		}
		public async Task InvokeAsync(HttpContext httpContext)
		{
            await DecryptData(httpContext);
            await LogRequest(httpContext);
            var originalResponseBody = httpContext.Response.Body;

            using (var responseBody = new MemoryStream())
            {
                httpContext.Response.Body = responseBody;

                await LogResponse(httpContext, responseBody, originalResponseBody);
            }
            try
			{
				await next(httpContext);
			}
			catch(Exception e)
			{

				var errorId = Guid.NewGuid();
				//log exception
				logger.LogError(e,$"{errorId}: {e.Message}");

				//return custom error repsone
				httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
				httpContext.Response.ContentType = "application/json";

				var error = new
				{
					Id = errorId,
					ErrorMessage = "Check the server logs"
				};

				await httpContext.Response.WriteAsJsonAsync(error);
			}
            
		}
        private async Task DecryptData(HttpContext context)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var requestRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();

                if (context.Request.ContentType == "application/json")
                {
                    context.Request.EnableBuffering(); // Enable buffering to read the request body
                    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
                    {
                        var requestBody = await reader.ReadToEndAsync();
                        context.Request.Body.Position = 0; // Reset the stream position

                        var encryptedData = JsonConvert.DeserializeObject<EncryptedData>(requestBody);
                        if (encryptedData != null)
                        {
                            string decryptedData = requestRepository.DecryptData(encryptedData.Data);
                            context.Items["DecryptedData"] = decryptedData; // Store the decrypted data in HttpContext
                        }
                    }
                }
            }

        }
        private async Task LogResponse(HttpContext context, MemoryStream responseBody, Stream originalResponseBody)
        {
            var responseContent = new StringBuilder();
            responseContent.AppendLine("=== Response Info ===");

            responseContent.AppendLine("-- headers");
            foreach (var (headerKey, headerValue) in context.Response.Headers)
            {
                responseContent.AppendLine($"header = {headerKey}    value = {headerValue}");
            }

            responseContent.AppendLine("-- body");
            responseBody.Position = 0;
            var content = await new StreamReader(responseBody).ReadToEndAsync();
            responseContent.AppendLine($"body = {content}");
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;

            logger.LogInformation(responseContent.ToString());
        }

        private async Task LogRequest(HttpContext context)
        {
            var requestContent = new StringBuilder();

            requestContent.AppendLine("=== Request Info ===");
            requestContent.AppendLine($"method = {context.Request.Method.ToUpper()}");
            requestContent.AppendLine($"path = {context.Request.Path}");

            requestContent.AppendLine("-- headers");
            foreach (var (headerKey, headerValue) in context.Request.Headers)
            {
                requestContent.AppendLine($"header = {headerKey}    value = {headerValue}");
            }

            requestContent.AppendLine("-- body");
            context.Request.EnableBuffering();
            var requestReader = new StreamReader(context.Request.Body);
            var content = await requestReader.ReadToEndAsync();
            requestContent.AppendLine($"body = {content}");

            logger.LogInformation(requestContent.ToString());
            context.Request.Body.Position = 0;
        }
    }
}

