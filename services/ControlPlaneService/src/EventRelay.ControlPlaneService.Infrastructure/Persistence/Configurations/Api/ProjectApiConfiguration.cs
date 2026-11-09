
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Api;

public sealed class ProjectApiConfiguration
    : IEntityTypeConfiguration<ProjectApiRecord>
{
    public void Configure(EntityTypeBuilder<ProjectApiRecord> b)
    {
        b.ToTable("project_api_keys");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        b.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(150)
            .IsRequired();

        b.Property(x => x.KeyHash)
            .HasColumnName("KeyHash")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.ProjectId)
            .HasColumnName("ProjectId")
            .IsRequired();

        b.Property(x => x.CreatedByUserId)
            .HasColumnName("CreatedByUserId")
            .IsRequired();

        b.Property(x => x.RevokedByUserId)
            .HasColumnName("RevokedByUserId")
            .IsRequired(false);

        b.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.RevokedAt)
            .HasColumnName("RevokedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // Indexes
        b.HasIndex(x => x.KeyHash).IsUnique();

        b.HasIndex(x => new
        {
            x.ProjectId,
            x.RevokedAt
        });

        // Relationships
        b.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.RevokedBy)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
