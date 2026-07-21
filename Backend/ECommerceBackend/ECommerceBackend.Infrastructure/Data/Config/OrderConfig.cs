using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class OrderConfig : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever(); // orderId supplied by the app

            builder.Property(o => o.UserId).IsRequired();
            builder.Property(o => o.Status).IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

            builder.HasIndex(o => o.UserId);   // fast "my orders" lookups
            builder.HasIndex(o => o.Status);   // reconciliation queries

            builder.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
