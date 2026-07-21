using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class OrderLineItemConfig : IEntityTypeConfiguration<OrderLineItem>
    {
        public void Configure(EntityTypeBuilder<OrderLineItem> builder)
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedOnAdd();

            builder.Property(i => i.OrderId).IsRequired();
            builder.Property(i => i.ProductId).IsRequired();
            builder.Property(i => i.Quantity).IsRequired();
            builder.Property(i => i.UnitPrice).IsRequired().HasPrecision(18, 2);
            builder.Property(i => i.Description).IsRequired();

            builder.HasIndex(i => i.OrderId); // fast load of an order's lines
        }
    }
}
