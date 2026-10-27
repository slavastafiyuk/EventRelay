using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Project;

public sealed class ProjectMemberConfiguration
    : IEntityTypeConfiguration<ProjectMemberRecord>
{
    public void Configure(
        EntityTypeBuilder<ProjectMemberRecord> builder)
    {
        builder.ToTable("project_members");

        builder.HasKey(x => new
        {
            x.ProjectId,
            x.UserId
        });

        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
    }
}
