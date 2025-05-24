using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Services;

//using ECommerceBackend.Application.Services;
using ECommerceBackend.Infrastructure.Data;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Text; // Add this using directive

var builder = WebApplication.CreateBuilder(args);

// Add controllers and configure JSON serialization settings (PascalCase)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to use PascalCase instead of camelCase
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Disable camelCase naming
    });

//builder.Services.AddScoped<IAuthService, AuthService>();
// Add EF Core with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ECommerceBackendDBConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();

// Register services
builder.Services.AddTransient<ICartService, CartService>();
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<ICheckoutService, CheckoutService>();
// Register OrderedItemService with parameters from configuration
builder.Services.AddScoped<IOrderedItemService>(provider =>
{
    var invoiceRepository = provider.GetRequiredService<IInvoiceRepository>();
    var blobConnectionString = builder.Configuration["AzureBlobStorage:ConnectionString"];
    var containerName = builder.Configuration["AzureBlobStorage:ContainerName"];

    // Ensure blobConnectionString and containerName are not null or empty
    if (string.IsNullOrEmpty(blobConnectionString))
    {
        throw new ArgumentNullException(nameof(blobConnectionString), "Azure Blob Storage connection string is not configured.");
    }

    if (string.IsNullOrEmpty(containerName))
    {
        throw new ArgumentNullException(nameof(containerName), "Azure Blob Storage container name is not configured.");
    }

    return new OrderedItemService(invoiceRepository, blobConnectionString: blobConnectionString, containerName);
});

// Register AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
var secret = jwtSettings["Secret"];
if(string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience) || string.IsNullOrEmpty(secret))
{
    throw new ArgumentNullException("JWT settings are not properly configured in appsettings.json");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            //ClockSkew = TimeSpan.Zero
        };
    });

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("http://localhost:3000") // Allow requests from your frontend's URL
               .AllowAnyMethod()  // Allow any HTTP method (GET, POST, PUT, DELETE, etc.)
               .AllowAnyHeader() // Allow any headers in the request
               .AllowCredentials();
    });
});

// Set QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Use CORS
app.UseCors("AllowFrontend"); // Apply the "AllowFrontend" policy globally

app.UseAuthorization();
app.MapControllers();
app.Run();