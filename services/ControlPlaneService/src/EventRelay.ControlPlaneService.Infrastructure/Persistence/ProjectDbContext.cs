using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class ProjectDbContext(
    DbContextOptions<ProjectDbContext> options)
    : DbContext(options)
{
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ProjectDbContext).Assembly);
    }
}
