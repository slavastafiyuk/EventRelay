namespace EventRelay.ControlPlaneService.Domain.Interfaces;

public interface ISecondaryKey<out T>
{
    T SecondaryKey { get; }
}
