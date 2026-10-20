using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference("/scalar", options =>
    {
        options
            .WithTitle("EventRelay API")
            .AddDocument(
                "controlplane",
                "Control Plane API",
                "/docs/specs/controlplane.json")
            .AddDocument(
                "events",
                "Event Ingestion API",
                "/docs/specs/events.json")
            .AddServer(
                "http://localhost:5000",
                "Local Gateway");
    });
}

app.MapReverseProxy();
app.Run();
