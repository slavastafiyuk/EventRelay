using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Application.Models;
using EventRelay.ControlPlaneService.Presentation.Handlers;

namespace EventRelay.ControlPlaneService.Tests;

public sealed class ProjectPingHandlerTests
{
    [Fact]
    public void Handle_MapsApplicationResultToResponse()
    {
        // Arrange
        var service = new FakeProjectStatusService();

        var handler = new PingHandler(service);

        // Act
        var response = handler.Handle();

        // Assert
        Assert.Equal("TestService", response.Service);
        Assert.Equal("ready", response.Status);
    }

    private sealed class FakeProjectStatusService
        : IStatusService
    {
        public ProjectStatus GetStatus()
        {
            return new ProjectStatus(
                Service: "TestService",
                Status: "ready");
        }
    }
}