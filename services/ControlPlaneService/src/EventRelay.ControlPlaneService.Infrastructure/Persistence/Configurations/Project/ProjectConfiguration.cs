using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Project;

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

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Owner is a UserKeyRecord populated by the repository,
        // not an EF Core navigation.
        builder.Ignore(x => x.Owner);

        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasOne(x => x.Access)
            .WithOne()
            .HasForeignKey<ProjectAccessRecord>("ProjectId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Navigation(x => x.Access)
            .IsRequired();
    }
}
