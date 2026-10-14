namespace EventRelay.ProjectService.Presentation.Contracts;

public sealed record ProjectPingResponse(
    string Service,
    string Status);