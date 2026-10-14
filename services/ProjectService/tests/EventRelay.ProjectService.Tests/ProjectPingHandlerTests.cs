using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Application.Models;
using EventRelay.ProjectService.Presentation.Handlers;

namespace EventRelay.ProjectService.Tests;

public sealed class ProjectPingHandlerTests
{
    [Fact]
    public void Handle_MapsApplicationResultToResponse()
    {
        // Arrange
        var service = new FakeProjectStatusService();

        var handler = new ProjectPingHandler(service);

        // Act
        var response = handler.Handle();

        // Assert
        Assert.Equal("TestService", response.Service);
        Assert.Equal("ready", response.Status);
    }

    private sealed class FakeProjectStatusService
        : IProjectStatusService
    {
        public ProjectStatus GetStatus()
        {
            return new ProjectStatus(
                Service: "TestService",
                Status: "ready");
        }
    }
}