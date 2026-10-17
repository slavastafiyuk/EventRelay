using EventRelay.ControlPlaneService.Application;
using EventRelay.ControlPlaneService.Infrastructure;
using EventRelay.ControlPlaneService.Presentation;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Mappings;
using EventRelay.ControlPlaneService.Repository;
using EventRelay.ControlPlaneService.Repository.Mappings;
using Mapster;

var config = TypeAdapterConfig.GlobalSettings;

config.Scan(
    typeof(ProjectPersistenceMapping).Assembly,
    typeof(ProjectApiMapping).Assembly);

config.RequireExplicitMapping = true;
config.Compile();

var builder = WebApplication.CreateBuilder(args);

// Dependency Injection
builder.Services.AddInfrastructure(
    builder.Configuration);
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
