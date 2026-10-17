namespace EventRelay.ControlPlaneService.Presentation.Contracts;

public sealed record ProjectPingResponse(
    string Service,
    string Status);