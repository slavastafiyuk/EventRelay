var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/api/projects/ping", () =>
    Results.Ok(new
    {
        service = "ProjectService",
        status = "ok"
    }));

app.Run();

public partial class Program { }