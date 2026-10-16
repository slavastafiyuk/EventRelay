using EventRelay.ProjectService.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ProjectService.Infrastructure.Persistence;

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
