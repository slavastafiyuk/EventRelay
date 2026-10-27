using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.User;

public sealed class UserKeyConfiguration : IEntityTypeConfiguration<UserKeyRecord>
{
    public void Configure(EntityTypeBuilder<UserKeyRecord> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(150)
            .IsRequired();

        // Both entity types represent the same users row.
        // UserRecord is the principal; UserKeyRecord
        // is the lightweight dependent.
        builder.HasOne<UserRecord>()
            .WithOne()
            .HasForeignKey<UserKeyRecord>(x => x.Id);
    }
}
