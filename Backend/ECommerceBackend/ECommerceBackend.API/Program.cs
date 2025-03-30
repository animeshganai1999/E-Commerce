using ECommerceBackend.Application.Interfaces;
//using ECommerceBackend.Application.Services;
using ECommerceBackend.Infrastructure.Data;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore; // Add this using directive

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
//builder.Services.AddScoped<IAuthService, AuthService>();
// Add EF Core with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ECommerceBackendDBConnection")));

builder.Services.AddTransient<IUserRepository, UserRepository>();
builder.Services.AddTransient<ICartRepository, CartRepository>();

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();
app.Run();
