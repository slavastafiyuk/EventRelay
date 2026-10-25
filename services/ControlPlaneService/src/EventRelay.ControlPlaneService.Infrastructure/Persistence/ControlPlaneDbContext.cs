using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class ControlPlaneDbContext(
    DbContextOptions<ControlPlaneDbContext> options)
    : DbContext(options)
{
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();
    public DbSet<UserRecord> Users => Set<UserRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ControlPlaneDbContext).Assembly);
    }
}
