using Expense.API.Data;
using Microsoft.EntityFrameworkCore;

public static class DatabaseConfig
{
    public static void ConfigureDatabases(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UserDocumentsDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("ExpenseConnectionString")));

        services.AddDbContext<ExpenseAuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("ExpenseAuthConnectionString")));
    }
}
