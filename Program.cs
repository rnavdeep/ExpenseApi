using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSWalks.API.Data;
using NSWalks.API.Mappings;
using NSWalks.API.Repositories;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using Serilog;
using NSWalks.API.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;

using Amazon.S3;
using Amazon.Textract;

using NSWalks.API.Repositories.Documents;
using NSWalks.API.Repositories.Users;
using NSWalks.API.Repositories.AuthToken;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//file over a new day, create new file
//write logs to console with minimum level of information
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/NSWalks_Log.txt",rollingInterval:RollingInterval.Day)
    .MinimumLevel.Information().CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);


builder.Services.AddControllers();
//?api-version = (1.0 or 2.0)
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

builder.Services.AddHttpContextAccessor();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "NZWalksApi", Version = "v1" });
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                },
                Scheme = "Oauth2",
                Name = JwtBearerDefaults.AuthenticationScheme,
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    }) ;
});

//db services
//webapi
builder.Services.AddDbContext<NZWalksDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("NZWalksConnectionString")));
builder.Services.AddDbContext<UserDocumentsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NZWalksConnectionString")));
//authentication
builder.Services.AddDbContext<NZWalksAuthDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("NZWalksAuthConnectionString")));


builder.Services.AddScoped<IRegionRepository, RegionRepository>();
builder.Services.AddScoped<IDifficultyRepository, DifficultyRepository>();
builder.Services.AddScoped<IWalksRepository, WalksRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IImagesRepository, ImagesRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddAutoMapper(typeof(AutomapperProfiles));

builder.Services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>()
    .AddTokenProvider<DataProtectorTokenProvider<IdentityUser>>("NZWalks")
    .AddEntityFrameworkStores<NZWalksAuthDbContext>()
    .AddDefaultTokenProviders();
//password settings
builder.Services.Configure<IdentityOptions>(options =>{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
});

// Configure JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Ensure cookies are sent over HTTPS
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.LoginPath = "/api/Auth/Login"; // Define the login path
});
// Configure AWS SDK
var c = builder.Configuration.GetAWSOptions("AWS");
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions("AWS"));
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonTextract>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
}
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
//WalkImages Configuration
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(),"Images/WalkImages")),
    RequestPath = "/Images/WalkImages"
});
//RegionImages configuration

// Configure the second set of static files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Images/RegionImages")),
    RequestPath = "/Images/RegionImages"
});
app.MapControllers();

app.Run();

