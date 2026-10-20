using System.Reflection;
using EventRelay.ControlPlaneService.Application;
using EventRelay.ControlPlaneService.Infrastructure;
using EventRelay.ControlPlaneService.Presentation;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Repository;
using Mapster;

var config = TypeAdapterConfig.GlobalSettings;

config.Scan(
    typeof(EventRelay.ControlPlaneService.Repository.Mappings.Mapping).Assembly,
    typeof(EventRelay.ControlPlaneService.Presentation.Mappings.Mapping).Assembly);

config.RequireExplicitMapping = true;
config.Compile();

var builder = WebApplication.CreateBuilder(args);

var isGeneratingOpenApi =
    Assembly.GetEntryAssembly()?.GetName().Name
    == "GetDocument.Insider";

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Dependency Injection
builder.Services.AddInfrastructure(
    builder.Configuration,
    isGeneratingOpenApi);
builder.Services.AddRepositories();
builder.Services.AddApplication();
builder.Services.AddPresentation();

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapHealthChecks("/health");

app.MapControllers();

app.MapGet(
    "/api/projects/ping",
    (IPingHandler handler) =>
    {
        var response = handler.Handle();

        return Results.Ok(response);
    });

app.Run();

public partial class Program { }
