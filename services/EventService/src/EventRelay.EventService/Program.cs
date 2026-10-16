using EventRelay.EventService.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("EventDatabase")
    ?? throw new InvalidOperationException(
        "EventDatabase connection string is missing.");

builder.Services.AddDbContext<EventDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/api/events/ping", () =>
    Results.Ok(new
    {
        service = "EventService",
        status = "ok"
    }));

app.Run();
