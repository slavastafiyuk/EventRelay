using EventRelay.ProjectService.Application;
using EventRelay.ProjectService.Infrastructure;
using EventRelay.ProjectService.Presentation;
using EventRelay.ProjectService.Presentation.Abstractions;
using EventRelay.ProjectService.Repository;

var builder = WebApplication.CreateBuilder(args);

// Dependency Injection
builder.Services.AddInfrastructure();
builder.Services.AddRepositories();
builder.Services.AddApplication();
builder.Services.AddPresentation();

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet(
    "/api/projects/ping",
    (IProjectPingHandler handler) =>
    {
        var response = handler.Handle();

        return Results.Ok(response);
    });

app.Run();

public partial class Program { }