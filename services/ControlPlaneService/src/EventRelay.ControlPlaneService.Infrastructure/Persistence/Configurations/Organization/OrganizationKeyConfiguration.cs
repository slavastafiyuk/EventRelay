using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Organization;

public sealed class OrganizationKeyConfiguration : IEntityTypeConfiguration<OrganizationKeyRecord>
{
    public void Configure(EntityTypeBuilder<OrganizationKeyRecord> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(150)
            .IsRequired();

        builder.HasOne<OrganizationRecord>()
            .WithOne()
            .HasForeignKey<OrganizationKeyRecord>(x => x.Id);
    }
}
