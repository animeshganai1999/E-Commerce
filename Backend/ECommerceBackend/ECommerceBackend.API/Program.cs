using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Storage.Blobs;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Options;
using ECommerceBackend.Application.Services;

//using ECommerceBackend.Application.Services;
using ECommerceBackend.Infrastructure.Data;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using StackExchange.Redis;
using System.Text; // Add this using directive
// Note: ConfigureForAzureWithTokenCredentialAsync is an extension method provided by
// the Microsoft.Azure.StackExchangeRedis package (enables passwordless Entra ID auth).

var builder = WebApplication.CreateBuilder(args);

// Integrate Azure Key Vault for secret management
// This allows the application to retrieve secrets from Azure Key Vault instead of appsettings.json
var keyVaultName = builder.Configuration["KeyVaultName"];
if (!string.IsNullOrEmpty(keyVaultName))
{
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential(),
        new AzureKeyVaultConfigurationOptions
        {
            // Optional: Configure reload interval if secrets need to be refreshed
            // ReloadInterval = TimeSpan.FromMinutes(5)
        });
}

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("ECommerceBackendDBConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null))); // retry on transient SQL faults

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<IProductCache, ProductCache>();
builder.Services.AddScoped<ICartCache, CartCache>();
// Register services
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IProductService, ProductService>();

// Register Key Vault service for direct secret access if needed
builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();

// Bind Azure Blob Storage settings using the Options pattern
builder.Services.AddOptions<AzureBlobOptions>()
    .Bind(builder.Configuration.GetSection(AzureBlobOptions.SectionName))
    .Validate(o => !string.IsNullOrEmpty(o.ConnectionString), "Azure Blob Storage connection string is not configured.")
    .Validate(o => !string.IsNullOrEmpty(o.ContainerName), "Azure Blob Storage container name is not configured.")
    .ValidateOnStart();

builder.Services.AddScoped<IOrderedItemService, OrderedItemService>();

// Register BlobServiceClient as a singleton
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureBlobOptions>>().Value;
    return new BlobServiceClient(options.ConnectionString);
});

// Redis connection (Azure Managed Redis - passwordless via Microsoft Entra ID / Managed Identity)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisHostName = builder.Configuration["Redis:HostName"];
    if (string.IsNullOrEmpty(redisHostName))
    {
        // Fallback to a raw connection string (e.g. local development with "localhost:6379")
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis")!;
        return ConnectionMultiplexer.Connect(redisConnectionString);
    }

    // Azure Managed Redis uses port 10000 and requires TLS
    var configurationOptions = ConfigurationOptions.Parse($"{redisHostName}:10000");
    configurationOptions.Ssl = true;
    configurationOptions.AbortOnConnectFail = false; // resilient: keep retrying instead of throwing on startup
    configurationOptions
        .ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential())
        .GetAwaiter().GetResult();

    return ConnectionMultiplexer.Connect(configurationOptions);
});

// Reservation repository
builder.Services.AddScoped<IStockReservationRepository, StockReservationRepository>();

// Background sweeper (runs on every instance, but the lock ensures only one sweeps)
builder.Services.AddHostedService<ReservationSweeperService>();

// Register AutoMapper
builder.Services.AddAutoMapper(cfg => { }, typeof(ECommerceBackend.API.Configuration.AutoMapperConfig).Assembly);

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
var secret = jwtSettings["Secret"];
if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience) || string.IsNullOrEmpty(secret))
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

builder.Services.AddAuthorization();

// Rate limiting is enforced at the API Gateway level (e.g., Azure API Management),
// so the in-app rate limiter has been removed.

// Enable CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins) // Allowed frontend URLs from configuration
               .AllowAnyMethod()  // Allow any HTTP method (GET, POST, PUT, DELETE, etc.)
               .AllowAnyHeader() // Allow any headers in the request
               .AllowCredentials();
    });
});

// Global exception handling (RFC 7807 ProblemDetails)
builder.Services.AddExceptionHandler<ECommerceBackend.API.Infrastructure.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Background outbox processor (crash-safe Redis + SQL stock settlement)
builder.Services.AddHostedService<ECommerceBackend.API.HostedServices.OutboxProcessorService>();

// Background reconciliation (self-heals Redis <-> SQL stock drift)
builder.Services.AddHostedService<ECommerceBackend.API.HostedServices.StockReconciliationService>();

// Health checks for SQL Server and Redis
var sqlConn = builder.Configuration.GetConnectionString("ECommerceBackendDBConnection");
var redisHostConfigured = !string.IsNullOrEmpty(builder.Configuration["Redis:HostName"]);
var redisConn = builder.Configuration.GetConnectionString("Redis");
var healthChecks = builder.Services.AddHealthChecks();
if (!string.IsNullOrEmpty(sqlConn))
    healthChecks.AddSqlServer(sqlConn, name: "sql-server");
if (redisHostConfigured)
{
    // Use the registered (passwordless) multiplexer for the Redis health check
    healthChecks.AddRedis(
        sp => sp.GetRequiredService<IConnectionMultiplexer>(),
        name: "redis");
}
else if (!string.IsNullOrEmpty(redisConn))
{
    healthChecks.AddRedis(redisConn, name: "redis");
}

// Configure forwarded headers so the correct client IP/scheme is seen behind a proxy/load balancer
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

// Set QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Honor forwarded headers (client IP / scheme) when behind a proxy or load balancer
app.UseForwardedHeaders();

// Global exception handling — returns ProblemDetails for unhandled exceptions
app.UseExceptionHandler();

// Redirect HTTP to HTTPS
app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowFrontend"); // Apply the "AllowFrontend" policy globally

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();