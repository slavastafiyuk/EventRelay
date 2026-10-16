using EventRelay.ProjectService.Application;
using EventRelay.ProjectService.Infrastructure;
using EventRelay.ProjectService.Presentation;
using EventRelay.ProjectService.Presentation.Abstractions;
using EventRelay.ProjectService.Presentation.Mappings;
using EventRelay.ProjectService.Repository;
using EventRelay.ProjectService.Repository.Mappings;
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
