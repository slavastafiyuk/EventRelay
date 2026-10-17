using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<ProjectRecord>
{
    public void Configure(
        EntityTypeBuilder<ProjectRecord> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("Description")
            .HasColumnType("text");

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}
