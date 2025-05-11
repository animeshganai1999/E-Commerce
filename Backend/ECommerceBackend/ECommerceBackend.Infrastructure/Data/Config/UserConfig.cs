using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class UserConfig : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.UserId); // Use UserId (GUID) as the primary key

            builder.Property(u => u.UserId)
                .IsRequired()
                .HasDefaultValueSql("NEWID()"); // Auto-generate GUID on insert

            builder.Property(u => u.Name).IsRequired();
            builder.Property(u => u.Email).IsRequired();
            builder.Property(u => u.PasswordHash).IsRequired();

            // Some Seed Data [Need to delete]
            builder.HasData(new List<User>()
            {
                new() {UserId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), Name = "Animesh", Email = "animesh@gmail.com", PasswordHash = "123456"},
                new() {UserId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3302"),Name = "Sayari", Email = "sayari@gmail.com", PasswordHash = "123456"}
            });
        }
    }
}
