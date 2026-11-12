using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Configurations.Organization;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMemberRecord>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberRecord> builder)
    {
        builder.ToTable("organization_members");
        builder.HasKey(x => new
        {
            x.OrganizationId,
            x.UserId
        });

        builder.Property(x => x.OrganizationId)
            .HasColumnName("OrganizationId")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("UserId")
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("Role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.JoinedAt)
            .HasColumnName("JoinedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
    }
}
