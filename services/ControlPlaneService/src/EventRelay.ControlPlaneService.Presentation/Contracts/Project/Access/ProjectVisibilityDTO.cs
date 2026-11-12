using System.Text.Json.Serialization;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;

[JsonConverter(typeof(JsonStringEnumConverter<ProjectVisibilityDTO>))]
public enum ProjectVisibilityDTO
{
    Public = 0,
    Private = 1,
    Internal = 2
}
