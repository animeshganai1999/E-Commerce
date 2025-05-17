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

// Register services
builder.Services.AddTransient<ICartService, CartService>();
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<ICheckoutService, CheckoutService>();

// Register AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // Validate the issuer of the token
            ValidateAudience = true, // Validate the audience of the token
            ValidateLifetime = true, // Validate if the token is expired
            ValidateIssuerSigningKey = true, // Validate if the signing key is correct

            // The issuer and audience must match what's in the token
            ValidIssuer = "yourdomain.com",  // Replace with your domain or a URL
            ValidAudience = "yourdomain.com",  // Replace with your domain or a URL

            // Key used for signing the JWT token
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("this_is_a_very_long_secret_key@2025!!"))  // Use your secret key here
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