using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Expense.API.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// AWS-backed endpoints, driven against the mocked IAmazonS3 / IAmazonTextract registered by the
/// factory. Covers the one cleanly-mockable happy path (S3 list-buckets) plus the authorization
/// gates on every AWS endpoint, and the missing guard on Document DELETE.
/// </summary>
public class AwsEndpointTests : IntegrationTestBase
{
    public AwsEndpointTests(CustomWebAppFactory factory) : base(factory) { }

    // NOTE: the S3 controller's route resolves to "api/S/list-buckets" (the [controller] token yields
    // "S", not "S3") — a real routing quirk worth being aware of when the frontend calls it.
    private const string ListBucketsUrl = "/api/S/list-buckets";

    [Fact]
    public async Task S3_list_buckets_returns_mocked_buckets_when_authenticated()
    {
        var user = await RegisterAndLoginAsync("s3");

        var res = await user.Client.GetAsync(ListBucketsUrl);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var buckets = await res.Content.ReadFromJsonAsync<List<JsonElement>>();
        buckets!.Should().ContainSingle();
        buckets[0].GetProperty("bucketName").GetString().Should().Be("expense-receipts-test-bucket");
    }

    [Theory]
    [InlineData(ListBucketsUrl)]
    [InlineData("/api/Document/downloadLinks")]
    [InlineData("/api/Document/download/receipt.pdf")]
    public async Task Aws_get_endpoints_require_authentication(string url)
    {
        var res = await Factory.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Textract_start_requires_authentication()
    {
        var res = await Factory.CreateClient()
            .PostAsync($"/api/Textract/startTextract?expenseGuid={Guid.NewGuid()}", null);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Document_upload_requires_authentication()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(Guid.NewGuid().ToString()), "ExpenseId" }
        };
        var res = await Factory.CreateClient().PostAsync("/api/Document/upload", content);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Document_delete_is_not_authorization_guarded()
    {
        // SECURITY GAP: DELETE /api/Document/{id} has no [Authorize]. An anonymous caller is therefore
        // not rejected with 401; instead the repository throws "User not logged in", which the
        // (catch-less) controller surfaces as a 500 via the exception middleware.
        var res = await Factory.CreateClient().DeleteAsync($"/api/Document/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
