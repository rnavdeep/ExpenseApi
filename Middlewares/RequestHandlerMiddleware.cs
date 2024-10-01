using System;
using Expense.API.Repositories.Request;
using Newtonsoft.Json;
using System.Text;
using System.Net;
using Expense.API.Models.Domain;

namespace Expense.API.Middlewares
{
	public class RequestHandlerMiddleware
	{
        private readonly RequestDelegate next;
        private readonly IServiceProvider serviceProvider;
        public RequestHandlerMiddleware(IServiceProvider serviceProvider,
            RequestDelegate next)
		{
            this.serviceProvider = serviceProvider;
            this.next = next;
		}
        public async Task InvokeAsync(HttpContext httpContext)
        {
            await DecryptData(httpContext);
            await next(httpContext);

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
    }
}

