using System.Diagnostics;

Console.Write("Migration name: ");
var name = Console.ReadLine()?.Trim();

if (string.IsNullOrWhiteSpace(name))
{
    Console.WriteLine("Migration name is required.");
    return;
}

// Find repository root.
var directory = new DirectoryInfo(AppContext.BaseDirectory);

while (directory != null && !File.Exists(Path.Combine(directory.FullName, "EventRelay.slnx")))
{
    directory = directory.Parent;
}

if (directory is null)
    throw new InvalidOperationException("Solution not found.");

var root = directory.FullName;
var project = Path.Combine(root, "services/ControlPlaneService/src/EventRelay.ControlPlaneService.Infrastructure");
var startup = Path.Combine(root, "services/ControlPlaneService/src/EventRelay.ControlPlaneService.Api");

var info = new ProcessStartInfo("dotnet")
{
    WorkingDirectory = root,
    UseShellExecute = false
};

foreach (var argument in new[]
{
    "ef", "migrations", "add", name,
    "--project", project,
    "--startup-project", startup,
    "--context", "ControlPlaneDbContext",
    "--output-dir", "Persistence/Migrations"
})
{
    info.ArgumentList.Add(argument);
}

const string connectionStringKey = "ConnectionStrings__ProjectDatabase";

if (!info.Environment.TryGetValue(connectionStringKey, out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
{
    info.Environment[connectionStringKey] = "Host=localhost;Database=unused;Username=dummy;Password=dummy";
}
using var process = Process.Start(info) ?? throw new InvalidOperationException("Failed to start dotnet.");

process.WaitForExit();

Console.WriteLine(
    process.ExitCode == 0
        ? "Migration generated successfully."
        : $"Migration failed: {process.ExitCode}");

Console.WriteLine("Press Enter to exit.");
Console.ReadLine();
