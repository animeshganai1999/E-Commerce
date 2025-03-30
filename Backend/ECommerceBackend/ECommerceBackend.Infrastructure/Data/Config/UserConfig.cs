using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class UserConfig : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id); // Specify the primary key
            builder.Property(u => u.Id)
                .ValueGeneratedOnAdd(); // Auto-increment the Id
            builder.Property(u => u.Email).IsRequired(); // Email is required
            builder.Property(u => u.PasswordHash).IsRequired(); // PasswordHash is required

            // Some Seed Data [Need to delete]
            builder.HasData(new List<User>()
            {
                new() {Id = 1, Email = "animesh@gmail.com", PasswordHash = "123456"},
                new() {Id = 2, Email = "sayari@gmail.com", PasswordHash = "123456"}
            });
        }
    }
}
