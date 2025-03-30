using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        // DbSets for your entities
        public DbSet<User> Users { get; set; } // Create a context file for User model
        public DbSet<CartItem> CartItems { get; set; } // Create a context file for CartItem model

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfig());
            modelBuilder.ApplyConfiguration(new CartItemConfig());
            base.OnModelCreating(modelBuilder);
        }
    }
}
