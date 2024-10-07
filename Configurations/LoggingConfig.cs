using Serilog;

public static class LoggingConfig
{
    public static void ConfigureLogging(this WebApplicationBuilder builder)
    {
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/Expense.txt", rollingInterval: RollingInterval.Day)
            .MinimumLevel.Information()
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger);
    }
}
