using EventRelay.DeliveryService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DeliveryDatabase")
    ?? throw new InvalidOperationException(
        "DeliveryDatabase connection string is missing.");

builder.Services.AddDbContext<DeliveryDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
