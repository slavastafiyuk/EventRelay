using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.User;

public class UserConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Issuer).HasColumnName("Issuer").HasMaxLength(512).IsRequired();
        builder.Property(x => x.Subject).HasColumnName("Subject").HasMaxLength(255).IsRequired();

        builder.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
    }
}
