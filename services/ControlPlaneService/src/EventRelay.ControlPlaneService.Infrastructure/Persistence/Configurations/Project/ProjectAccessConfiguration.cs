using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Project;

public sealed class ProjectAccessConfiguration : IEntityTypeConfiguration<ProjectAccessRecord>
{
    public void Configure(EntityTypeBuilder<ProjectAccessRecord> builder)
    {
        builder.ToTable("project_access");

        builder.Property<Guid>("ProjectId");

        builder.HasKey("ProjectId");

        builder.Property(x => x.Visibility)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
