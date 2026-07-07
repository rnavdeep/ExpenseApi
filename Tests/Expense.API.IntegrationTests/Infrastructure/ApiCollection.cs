using Xunit;

namespace Expense.API.IntegrationTests.Infrastructure;

/// <summary>
/// All test classes join this collection so they share a single set of containers
/// (built once) and run sequentially, avoiding cross-test database contention.
/// Per-test isolation is achieved by giving every test its own unique users.
/// </summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<CustomWebAppFactory>
{
}
