var builder = WebApplication.CreateBuilder(args);

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