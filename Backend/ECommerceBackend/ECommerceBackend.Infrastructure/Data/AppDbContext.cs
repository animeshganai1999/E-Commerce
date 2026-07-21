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
        public DbSet<RefreshToken> RefreshTokens { get; set; } // Create a context file for RefreshToken model
        public DbSet<UserInvoice> UserInvoice { get; set; } // Create a context file for CustomerInvoice model
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLineItem> OrderItems { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        // Override the OnModelCreating method to apply configurations
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfig());
            modelBuilder.ApplyConfiguration(new CartItemConfig());
            modelBuilder.ApplyConfiguration(new RefreshTokenConfig());
            modelBuilder.ApplyConfiguration(new UserInvoiceConfig());
            modelBuilder.ApplyConfiguration(new ProductConfig());
            modelBuilder.ApplyConfiguration(new OrderConfig());
            modelBuilder.ApplyConfiguration(new OrderLineItemConfig());
            modelBuilder.ApplyConfiguration(new OutboxMessageConfig());
            base.OnModelCreating(modelBuilder);
        }
    }
}
