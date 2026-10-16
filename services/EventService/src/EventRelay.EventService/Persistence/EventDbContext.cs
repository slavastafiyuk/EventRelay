using Microsoft.EntityFrameworkCore;

namespace EventRelay.EventService.Persistence;

public sealed class EventDbContext(
    DbContextOptions<EventDbContext> options)
    : DbContext(options)
{
}
