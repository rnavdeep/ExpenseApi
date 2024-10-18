using Expense.API.Repositories.AuthToken;
using Expense.API.Repositories.Documents;
using Expense.API.Repositories.Expense;
using Expense.API.Repositories.ExpenseAnalysis;
using Expense.API.Repositories.Notifications;
using Expense.API.Repositories.QueryBuilder;
using Expense.API.Repositories.Redis;
using Expense.API.Repositories.Request;
using Expense.API.Repositories.Users;

public static class RepositoryConfig
{
    // Static method that registers repositories to the DI container
    public static void ConfigureRepositories(this IServiceCollection services)
    {
        services.AddScoped<ITokenRepository, TokenRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseAnalysis, ExpenseAnalysis>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRedisRepository, RedisRepository>();
        services.AddScoped<ITextractNotification, TextractNotification>();
        services.AddScoped(typeof(QueryBuilder<>));
    }
}
