using Microsoft.EntityFrameworkCore;

namespace EventRelay.DeliveryService;

public sealed class DeliveryDbContext(
    DbContextOptions<DeliveryDbContext> options)
    : DbContext(options)
{
}
