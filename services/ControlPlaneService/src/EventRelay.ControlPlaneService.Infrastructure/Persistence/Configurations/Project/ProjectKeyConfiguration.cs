using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Project;


public sealed class ProjectKeyConfiguration
    : IEntityTypeConfiguration<ProjectKeyRecord>
{
    public void Configure(EntityTypeBuilder<ProjectKeyRecord> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(150)
            .IsRequired();

        builder.HasOne<ProjectRecord>()
            .WithOne()
            .HasForeignKey<ProjectKeyRecord>(x => x.Id);
    }
}

