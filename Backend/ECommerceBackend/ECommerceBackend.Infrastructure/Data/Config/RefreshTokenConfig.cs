using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Identity.Client;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(ci => ci.Id); // Specify the primary key
            builder.Property(ci => ci.Id)
                .ValueGeneratedOnAdd(); // Specify that the Id is auto-generated
            builder.Property(ci => ci.UserId)
                .IsRequired(); // Specify that UserId is required
            builder.Property(ci => ci.Token)
                .IsRequired(); // Specify that Token is required
            builder.Property(ci => ci.ExpiryDate)
                .IsRequired(); // Specify that ExpiryDate is required
            builder.Property(ci => ci.IsRevoked)
                .IsRequired(); // Specify that IsRevoked is required
            builder.Property(ci => ci.CreatedAt)
                .IsRequired(); // Specify that CreatedAt is required
            builder.Property(ci => ci.RevokedAt)
                .IsRequired(false);
            builder.Property(ci => ci.ReplacedByToken)
                .IsRequired(false);
            builder.Property(ci => ci.UserAgent)
                .IsRequired(false); // Specify that UserAgent is optional

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Cascade) // Specify the foreign key relationship with User
                .HasConstraintName("FK_RefreshTokens_Users_UserId"); // Specify the constraint name
        }
    }
}
