using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class OutboxMessageConfig : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Type).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Payload).IsRequired();
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.Error).HasMaxLength(2000);

            builder.HasIndex(x => new { x.Type, x.AggregateId })
                .IsUnique()
                .HasFilter("[AggregateId] IS NOT NULL");
            builder.HasIndex(x => new { x.ProcessedAt, x.FailedAt, x.NextAttemptAt, x.CreatedAt });
        }
    }
}
