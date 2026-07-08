using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Expense.API.Data
{
    /// <summary>
    /// Lets `dotnet ef migrations add --context UserDocumentsDbContext` build the context without
    /// booting Program.cs (which connects to Redis eagerly at startup). The connection string is
    /// only used to select the SQL Server provider for schema diffing; it is never opened.
    /// </summary>
    public class UserDocumentsDbContextFactory : IDesignTimeDbContextFactory<UserDocumentsDbContext>
    {
        public UserDocumentsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UserDocumentsDbContext>();
            optionsBuilder.UseSqlServer("Server=localhost;Database=ExpenseAnalyserDb;Trusted_Connection=True;");
            return new UserDocumentsDbContext(optionsBuilder.Options);
        }
    }
}
