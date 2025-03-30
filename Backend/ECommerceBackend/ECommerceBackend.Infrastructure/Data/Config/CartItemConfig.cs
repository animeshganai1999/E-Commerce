using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class CartItemConfig : IEntityTypeConfiguration<CartItem>
    {
        public void Configure(EntityTypeBuilder<CartItem> builder)
        {
            builder.HasKey(ci => ci.Id); // Specify the primary key
            builder.Property(ci => ci.Id)
                .ValueGeneratedOnAdd(); // Auto-increment the Id
            builder.Property(ci => ci.Quantity).IsRequired(); // Quantity is required
            builder.Property(ci => ci.UserId).IsRequired(); // UserId is required
            builder.Property(ci => ci.ProductId).IsRequired(); // ProductId is required

            // Some Seed Data [Need to delete]
            builder.HasData(new List<CartItem>()
            {
                new() {Id = 1, UserId = 1, ProductId = 10, Quantity = 1},
                new() {Id = 2, UserId = 1, ProductId = 11, Quantity = 2}
            });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Cascade) // Set up the foreign key relationship with User
                .HasConstraintName("FK_Users_CartItems");
        }
    }
}
