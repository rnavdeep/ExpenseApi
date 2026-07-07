using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Expense.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// Boots the real API pipeline (Program.cs) against throwaway SQL Server + Redis containers,
/// with the AWS S3/Textract clients replaced by mocks so AWS-backed endpoints can be exercised
/// without real credentials. One SQL container hosts both databases (AuthenticationDb +
/// ExpenseAnalyserDb); the business schema is generated from the EF model (EnsureCreated) and the
/// Identity schema from the existing migration (Migrate), sidestepping the raw DDL scripts.
/// </summary>
public class CustomWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder().Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();

    /// <summary>Mock S3 client shared by the host; tests can add setups on it.</summary>
    public Mock<IAmazonS3> S3Mock { get; } = new(MockBehavior.Loose);

    /// <summary>Mock Textract client shared by the host; tests can add setups on it.</summary>
    public Mock<IAmazonTextract> TextractMock { get; } = new(MockBehavior.Loose);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync());

        var baseConn = _sql.GetConnectionString(); // Database=master
        var expenseConn = baseConn.Replace("Database=master", "Database=ExpenseAnalyserDb");
        var authConn = baseConn.Replace("Database=master", "Database=AuthenticationDb");

        // These are read by Program.cs *before* Build() (Redis connects eagerly), so they must be
        // process environment variables rather than ConfigureAppConfiguration entries.
        Environment.SetEnvironmentVariable("ConnectionStrings__ExpenseConnectionString", expenseConn);
        Environment.SetEnvironmentVariable("ConnectionStrings__ExpenseAuthConnectionString", authConn);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        // Issuer == Audience on purpose: JwtConfig validates the audience against Jwt:Issuer.
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-at-least-32-chars-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ExpenseApiTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ExpenseApiTests");
        Environment.SetEnvironmentVariable("AWS__Region", "us-east-1");
        Environment.SetEnvironmentVariable("AWS__BucketName", "expense-receipts-test-bucket");

        // A sensible default so S3Controller/list-buckets returns a payload without per-test setup.
        S3Mock.Setup(s => s.ListBucketsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ListBucketsResponse
              {
                  Buckets = new List<S3Bucket> { new() { BucketName = "expense-receipts-test-bucket" } }
              });

        // Force host build, then provision both database schemas.
        using var scope = Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<ExpenseAuthDbContext>();
        await auth.Database.MigrateAsync();
        var business = scope.ServiceProvider.GetRequiredService<UserDocumentsDbContext>();
        await business.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Don't let the background Textract poller hammer the mocked AWS client on a loop.
            var poller = services.SingleOrDefault(d =>
                d.ImplementationType == typeof(Expense.API.Repositories.Background.TextractPollingRepository));
            if (poller is not null) services.Remove(poller);

            // Swap the real AWS clients for mocks.
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton(S3Mock.Object);
            services.RemoveAll<IAmazonTextract>();
            services.AddSingleton(TextractMock.Object);

            // Re-register the business context so EnsureCreated can materialise the schema despite the
            // model's overlapping cascade-delete paths (see RestrictCascadeModelCustomizer).
            var options = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<UserDocumentsDbContext>));
            if (options is not null) services.Remove(options);
            services.AddDbContext<UserDocumentsDbContext>(o => o
                .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__ExpenseConnectionString"))
                .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer,
                    Infrastructure.RestrictCascadeModelCustomizer>());
        });
    }

    public new async Task DisposeAsync()
    {
        await _sql.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
