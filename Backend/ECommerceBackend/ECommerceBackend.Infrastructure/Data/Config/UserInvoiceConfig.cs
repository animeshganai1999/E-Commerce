using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class UserInvoiceConfig : IEntityTypeConfiguration<UserInvoice>
    {
        public void Configure(EntityTypeBuilder<UserInvoice> builder)
        {
            builder.HasKey(ui => ui.Id); // Specify the primary key
            builder.Property(ui => ui.Id)
                .ValueGeneratedOnAdd(); // Specify that the InvoiceId is auto-generated
            builder.Property(ui => ui.UserId)
                .IsRequired(); // Specify that UserId is required
            builder.Property(ui => ui.InvoiceDate)
                .IsRequired(); // Specify that InvoiceDate is required
            builder.Property(ui => ui.NumberOfItems)
                .IsRequired(); // Specify that NumberOfItems is required
            builder.Property(ui => ui.TotalAmount)
                .IsRequired() // Specify that TotalAmount is required
                .HasPrecision(18, 2); // Specify the precision for TotalAmount
            builder.Property(ui => ui.InvoiceLink)
                .IsRequired(); // Specify that TotalAmount is required

            // Add any additional configurations as needed
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(ui => ui.UserId)
                .OnDelete(DeleteBehavior.Cascade) // Specify the foreign key relationship with User
                .HasConstraintName("FK_Users_CustomerInvoices_UserId"); // Specify the constraint name
        }
    }
}
