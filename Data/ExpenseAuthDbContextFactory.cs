using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Expense.API.Data
{
    /// <summary>
    /// Lets `dotnet ef database update --context ExpenseAuthDbContext` build the context without
    /// booting Program.cs (which connects to Redis eagerly at startup). The connection string is
    /// only used to select the SQL Server provider for schema diffing; it is never opened.
    /// </summary>
    public class ExpenseAuthDbContextFactory : IDesignTimeDbContextFactory<ExpenseAuthDbContext>
    {
        public ExpenseAuthDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ExpenseAuthDbContext>();
            optionsBuilder.UseSqlServer("Server=localhost;Database=AuthenticationDb;Trusted_Connection=True;");
            return new ExpenseAuthDbContext(optionsBuilder.Options);
        }
    }
}
