using Expense.API.Data;
using Expense.API.Mappings;
using Expense.API.Middlewares;
using Expense.API.Repositories.Background;
using Expense.API.Repositories.Notifications;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Logging
builder.ConfigureLogging();

// Configure Services
builder.Services.AddControllers();

// Configure Redis as a Distributed Cache
var redisConnection = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));


// Add session services
builder.Services.AddDistributedMemoryCache(); // Required for session management
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Essential for GDPR
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

// Configure external services
builder.Services.ConfigureDatabases(builder.Configuration);
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.ConfigureSwagger();
builder.Services.ConfigureCors();
builder.Services.ConfigureAwsServices(builder.Configuration);
builder.Services.AddHostedService<TextractPollingRepository>();
builder.Services.AddSignalR();
// Configure AutoMapper and Identity
builder.Services.AddAutoMapper(typeof(AutomapperProfiles));
builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddTokenProvider<DataProtectorTokenProvider<IdentityUser>>("NZWalks")
    .AddEntityFrameworkStores<ExpenseAuthDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
});

// Configure Repositories
builder.Services.ConfigureRepositories();

var app = builder.Build();

// Middleware and HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseMiddleware<RequestHandlerMiddleware>();

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseRouting();
app.MapHub<TextractNotificationHub>("/api/textractNotification");
app.UseSession();
//app.Use(async (context, next) =>
//{
//    await next.Invoke();

//    var endpoints = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints;

//    foreach (var endpoint in endpoints)
//    {
//        Console.WriteLine($"Endpoint: {endpoint.DisplayName}");
//    }
//});


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
