namespace EventRelay.ControlPlaneService.Application.Models;

public sealed record ProjectStatus(
    string Service,
    string Status);